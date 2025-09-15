import React, { useEffect, useState } from 'react'
import { GeneralForm } from '../components/GeneralForm.tsx';
import {schema} from '../interfaces/interfaces.ts'
import { classroomFormFormat } from '../utils/FormatForm.ts'
import styles from '../pages/CSS/ClassRoomsPage.module.css'
import { Dialog } from '../components/Dialog.tsx';
import { CSS } from '../utils/Functions.ts';
import { ClassRoom } from '../interfaces/interfaces.ts';
import { GETAboutFile } from '../services/mockRequest.ts';


interface Props{
        classroomCode: string | null,
        handleSubmitClassRoomForm: (form:FormData ,data:ClassRoom)=>Promise<void>,
        deleteClassRoom : (evaluation: string)=>void,
        clean_frame?: () => void
}

export function ClassRoomForm({classroomCode,handleSubmitClassRoomForm,deleteClassRoom,clean_frame} :Props): JSX.Element {
    const [form_schema, setFormSchema] = useState<schema[]>([]);
    const [confirmation, setConfirmation] = useState<boolean>(false);   

    useEffect(() => {
        async function fetchFormSchema() {
                    const schema: schema[] = await classroomFormFormat(classroomCode as string);
                    console.log("Fetched schema:", schema);
                    setFormSchema(schema);
                }
            fetchFormSchema();   
    }, []);

    const addSchedule = () => {
        const newSchema = [...form_schema];
        newSchema.push({
            label: 'day',
            value: '',
            type: 'days'})
        newSchema.push({
            label: 'time',
            value: '',
            type: 'time'})

        setFormSchema(newSchema);
    }

     async function downloadFunction(label:string,filename:string):Promise<void>{
        if(label==='about'){
            const blob_result = await GETAboutFile(classroomCode as string);
            const link = document.createElement('a');
            const url = window.URL.createObjectURL(blob_result);
            link.href = url;
            link.download = filename;
            
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            window.URL.revokeObjectURL(url);
        }
    }

    async function FormFormated(event: React.FormEvent<HTMLFormElement>): Promise<void>{
            event.preventDefault();
            const form = event.target as HTMLFormElement;
            const formData = new FormData(form);
            const days: {day: string; time: string}[] = []
            const data: ClassRoom = {
                classroomCode:'',
                schedule: days,
                courseName: '',
                about: ''
            };   
            console.log("Form data:", formData.get('about'));
            formData.forEach((value, key: string) => 
                {if(key.includes('day') && value!=='None'){
                days.push({day: value as string, time: formData.get(`time-start${parseInt(key.replace('day', ''))+1}`) as string 
                                                + '-' + formData.get(`time-end${parseInt(key.replace('day', ''))+1}`) as string})
                }else if(!key.includes('time')){
                data[key] = value
                }
                });
               console.log(data.classroomCode);
            if(formData.get('about')){
               const file = formData.get('about') as File;
               formData.append('aboutFile',file);
               const fileName: string = file.name;
               formData.set('about', fileName);
               data['about'] = fileName;
            }
            data['schedule'] =  days;
            const jsonDays = JSON.stringify(days);
         
            formData.set('schedule',jsonDays);
            console.log("Data to be sent:", data);

            handleSubmitClassRoomForm(formData,data);

    }
    return (        <>
        {!confirmation ?
             <GeneralForm formSchemas ={form_schema} 
                    handleSubmit ={FormFormated} downloadFunction={downloadFunction} >   
                    <button className={CSS(styles,'add-date-button')} onClick= {(event)=>{
                        event.preventDefault();
                        addSchedule();
                        }}>Add Schedule</button>
                    {classroomCode?
                    <button className={CSS(styles,'delete')}            
                        onClick= {()=>{setConfirmation(true)}}>Delete Classroom
                    </button>:<></>}
            </GeneralForm>:
            <><Dialog isOpen={true} 
                onAccept={()=>{deleteClassRoom(classroomCode as string); clean_frame?.()}} 
                onCancel={()=>{setConfirmation(false)}}>
                <h1>Are you sure you want to delete this classroom?</h1>
            </Dialog></>}
        </>
    );


}    //     {!confirmation ?
import React, { useEffect, useState } from 'react'
import { GeneralForm } from '../components/GeneralForm.tsx';
import { GETrubricFile,GETstatementFile } from '../services/APIRequest.ts';
import {schema} from '../interfaces/interfaces.ts'
import { evaluationFormFormat } from '../utils/FormatForm.ts'
import styles from '../pages/CSS/EvaluationPage.module.css'
import { Dialog } from '../components/Dialog.tsx';
import { CSS } from '../utils/Functions.ts';
interface Props{
        name: string | null,
        classroomCode?: string | null,
        handleEditEvaluation: (FormData: FormData)=>Promise<void>,
        handleNewEvaluation: (formData:FormData)=>Promise<void>
        deleteEvaluation : (evaluation: string)=>void,
        clean_frame?: () => void
}

export function EvaluationForm({name,classroomCode,handleEditEvaluation,handleNewEvaluation,deleteEvaluation} :Props): JSX.Element {
    const [form_schema, setFormSchema] = useState<schema[]>([]);
    const [confirmation, setConfirmation] = useState<boolean>(false);
    useEffect(() => {
        async function fetchFormSchema() {
                    const schema: schema[] = await evaluationFormFormat(classroomCode as string,name );
                    setFormSchema(schema);
                }
            fetchFormSchema();   
    }, []);

    
async function NewEvaluation(event:  React.FormEvent<HTMLFormElement>): Promise<void>{
            event.preventDefault();
            const form = event.target as HTMLFormElement;
            let formData = new FormData(form);
            formData = changeFormFile(formData);
            handleNewEvaluation(formData);
}

async function EditEvaluation(event:  React.FormEvent<HTMLFormElement>): Promise<void>{
            event.preventDefault();
            const form = event.target as HTMLFormElement;
            let formData = new FormData(form);
            formData = changeFormFile(formData);
            handleEditEvaluation(formData);
}

     async function downloadFunction(label:string,filename:string):Promise<void>{
        let blob_result: Blob = new Blob();
        if(label==='statement'){
            blob_result = await GETstatementFile(classroomCode as string,name as string);
        }
        else if(label==='rubric'){
            blob_result = await GETrubricFile(classroomCode as string,name as string);

        }
        if(filename){
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

function changeFormFile(formData: FormData):FormData{

        if(formData.get('statement')){
            const file = formData.get('statement') as File;
            formData.append('statementFile',file);
            const fileName: string = file.name;
            formData.set('statement', fileName);
        }
        if(formData.get('rubric')){
            const file = formData.get('rubric') as File;
            formData.append('rubricFile',file);
            const fileName: string = file.name;
            formData.set('rubric', fileName);
        }
        return formData;
}
    return (
        <>
        {!confirmation ?
             <GeneralForm formSchemas ={form_schema} downloadFunction={downloadFunction}
                    handleSubmit ={name ? EditEvaluation : NewEvaluation}>
                    {name?
                    <button className={CSS(styles,'delete')}            
                        onClick= {()=>{setConfirmation(true)}}>Delete Evaluation
                    </button>:<></>}
            </GeneralForm>:
            <><Dialog isOpen={true} 
                onAccept={()=>{deleteEvaluation(name as string)}} 
                onCancel={()=>{setConfirmation(false)}}>
                <h1>Are you sure you want to delete this evaluation?</h1>
            </Dialog></>}
        </>
    )
}
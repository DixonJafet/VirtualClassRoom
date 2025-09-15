import React, { useLayoutEffect,useEffect, useState,useRef,ReactNode } from 'react'
import { GeneralForm } from '../components/GeneralForm.tsx';
import {DynamicObject,schema} from '../interfaces/interfaces.ts'
import { gradeFormFormat } from '../utils/FormatForm.ts'
import { EDITGradeInfo, GETFeedBakcFile } from '../services/mockRequest.ts';
interface Props {
        name?: string,
        row?:string,
        column?: string,
        evaluation: string,
        children?: ReactNode,
        classRoomCode: string,
        clean_frame?: () => void,
        editData: (data: DynamicObject,metadata: DynamicObject,formData: FormData)=>Promise<void>
}
export function GradeForm({row,column,name,evaluation,classRoomCode,children,clean_frame,editData} 
                            :Props): JSX.Element {
        const [form_schema, setFormSchema] = useState<schema[]>([]);
        useEffect(() => {
                async function fetchFormSchema() {
                            const schema: schema[] = await gradeFormFormat(classRoomCode, name, evaluation);
                            setFormSchema(schema);
                        }
                    fetchFormSchema();
        }, []);

        useEffect(() => {
        //     const timer = setTimeout(() => {
        const gradeInput = document.getElementById('grade0') as HTMLInputElement;
            if (gradeInput) {
                const maxSchema = form_schema.find(item => item.label === "max");
                const maxValue = maxSchema?.value;
                gradeInput.max = String(maxValue);
                gradeInput.focus();
            }
                //    }, 0);
                    //   return () => clearTimeout(timer);
        }, [form_schema]);


    async function handleGrade(event:  React.FormEvent<HTMLFormElement>): Promise<void>{
            event.preventDefault();
           const form = event.target as HTMLFormElement;
           const formData = new FormData(form);
            if(formData.get('feedback')){
               const file = formData.get('feedback') as File;
               formData.append('feedbackFile',file);
               const fileName: string = file.name;
               formData.set('feedback', fileName);
            }
           const data: DynamicObject = {};
            formData.forEach((value, key: string) => {data[key] = value as string});
            if(name){   
                editData(data,{
                    row : row as string,
                    column: column as string,
                    name
                }, formData);
            } else{}//create new grade  
            if (clean_frame) {
                clean_frame();
            }
        }



        
             async function downloadFunction(label:string,filename:string):Promise<void>{
                if(label==='feedback'){
                    const blob_result = await GETFeedBakcFile(classRoomCode,name as string,evaluation);
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

    return ( <><GeneralForm formSchemas ={form_schema} handleSubmit ={handleGrade} 
                                                        downloadFunction={downloadFunction} >
                    {children}
                </GeneralForm></>
    );
}
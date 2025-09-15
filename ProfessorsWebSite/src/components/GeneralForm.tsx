/* eslint-disable @typescript-eslint/no-unused-vars */
/* eslint-disable prefer-const */
/* eslint-disable no-unused-vars */
import styles from './CSS/GeneralForm.module.css'
import React, {ReactNode, useId} from 'react'
import {schema} from '../interfaces/interfaces.ts'
import { CSS } from '../utils/Functions.ts'
import { InputFile,InputNumber,InputText,InputDate,InputDays, InputTime, InputEmail, InputPhone, InputPassword } from './InputCustom.tsx'

interface Props{
    handleSubmit?: (event: React.FormEvent<HTMLFormElement>)=>Promise<void>,
    formSchemas: schema[],
    children?: ReactNode,
    submitText?: string,
    downloadFunction?: (label:string,value: string)=>void
}
export const GeneralForm: React.FC<Props> = ({
    handleSubmit,
    formSchemas,
    children,
    submitText,
    downloadFunction
}) =>{
    return(
        <>    
        <form className={CSS(styles,"form-container")} onSubmit={handleSubmit} >
            <div className={CSS(styles,"form-grid")}>
              {formSchemas.map((schema: schema,index:number)=>
              {const newLabel:string= schema.label.replace(/([a-z])([A-Z])/g, '$1 $2') /*this is just to make sure the label is readable*/
              return( schema.type!=='hidden'?
              <div className={CSS(styles,"form-group")} key={schema["label"]+`${index}`}>
               <label key={schema.label+`${index}`}  htmlFor={schema.label+`${index}`}>
                  {(schema.label.charAt(0).toUpperCase() + schema.label.slice(1)).replace(/([a-z])([A-Z])/g, '$1 $2').replace(/_/g, " ")}
                </label>
                  {schema.type==='text'? 
                  <InputText index = {`${index}`}   label={schema.label} value = {schema.value}/>:
                  schema.type==='number'?
                  <InputNumber index = {`${index}`} label={schema.label} value = {schema.value}/>:
                  schema.type==='file'?
                  <InputFile index = {`${index}`}   label={schema.label} value = {schema.value}  downloadFunction = {downloadFunction}/>:
                  schema.type==='date'?
                  <InputDate index = {`${index}`}   label={schema.label} value = {schema.value} />:
                  schema.type==='days'?
                  <InputDays index = {`${index}`}   label={schema.label} value = {schema.value} />:
                  schema.type==='email'?
                  <InputEmail index = {`${index}`}  label={schema.label} value = {schema.value} />:
                  schema.type==='phone'?
                  <InputPhone index = {`${index}`}  label={schema.label} value = {schema.value} />:
                  schema.type==='time'?
                  <InputTime index = {`${index}`}   label={schema.label} value = {schema.value} />:
                  schema.type==='password'?
                  <InputPassword index = {`${index}`} label={schema.label} value = {schema.value} />:
                  <></>}
                  
              </div>: <input type="hidden" id={schema.label+`${index}`} name={schema.label} value={schema.value} />)})} 
            </div>
                <button type="submit" className={CSS(styles,"submit-button")}>
                  {submitText?submitText:'Save'}
                </button>
                <br/>
                {children}
        </form></>
    )
}







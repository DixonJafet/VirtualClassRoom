/* eslint-disable no-unused-vars */

import styles from './CSS/InputCustom.module.css'
import { CSS } from '../utils/Functions'
import { useState } from 'react'
interface Props{label: string, value?: string  | number, index?: string}

export const InputFile= ({value,label,downloadFunction,index} : Props & {downloadFunction?: (label:string,value:string)=>void}): JSX.Element =>{
    const[thisValue,setThisValue] = useState(value);
    function setUploadDoc(){
        const fileInput = document.getElementById(`${label+index}`) as HTMLInputElement;
        const fileName = fileInput.files?.length
        ? fileInput.files[0].name
        : "No file chosen";
        setThisValue(fileName);


    }
    return(<>
        <label className = {CSS(styles,"custom-label")} htmlFor={label+index}>{`upload new ${label}`}</label>          
        <input className = {CSS(styles,"custom-file" )} 
        type="file" 
        id={label+index} 
        name={label} 
        accept = {'application/pdf'}
        onChange={setUploadDoc}/>   
        <span className={CSS(styles,"file-name")} id = {`uploaded-file-${label}`}>
        {thisValue ? 
        <>current: <a onClick={() => { if (downloadFunction) downloadFunction(label,value as string); }} href="#">{thisValue}</a>
        <span onClick = {()=>{setThisValue("")}} style = {{color:"red", cursor: "pointer"}}>x</span></> 
        : 'No file chosen'}</span> 
        
        </>
    )
        
    
}

export const InputDays = (props: {label: string, value: string | undefined | number ,index?: string})=>{
    return(<>
      <select id={props.label+props.index}
              name={props.label+props.index}
              defaultValue={props.value || 'None'}
              className={CSS(styles,"custom-select")}>
        <option value="None">None</option>
        <option value="Monday">Monday</option>
        <option value="Tuesday">Tuesday</option>
        <option value="Wednesday">Wednesday</option>
        <option value="Thursday">Thursday</option>
        <option value="Friday">Friday</option>
        <option value="Saturday">Saturday</option>
        <option value="Sunday">Sunday</option>
        
      </select>
    </>)
}

export const InputText = (props: {label: string, value: string | undefined | number ,index?: string})=>{
    return(<>
            <input type={'text'} 
            id={props.label+props.index} 
            name={props.label} 
            placeholder={`Enter a ${props.label.replace(/([a-z])([A-Z])/g, '$1 $2')}`} 
            defaultValue = {props.value || ''}
      
            />
        </>)}


export function InputNumber({label,value,index} : Props): JSX.Element{

    return(<>
            <input type={'number'} id={label+index} name={label} 
            placeholder={`Enter a ${label.replace(/([a-z])([A-Z])/g, '$1 $2')}`} 
            min = {'0'}
            step = {'1'}

            defaultValue = {Number(value)}/>
        </>)}

export function InputDate({label,value,index} : Props): JSX.Element{
    const today = new Date().toISOString().split("T")[0];
    value = String(value).split("T")[0];
    return(<>
        <input type={'date'} id={label+index} min={today} max={'2026-01-01'}
            defaultValue={value} 
            name={label}
        
        />
    
    </>)
}





export function InputTime({label,value,index} : Props): JSX.Element{
    const [start,end] = value ? (value as string).split('-') : ['','']
    return(<>
    <div>
        <input className={CSS(styles,"custom-time")} type={'time'} id={`${label}-start`+index} name={`${label}-start`+index} 
        defaultValue={start}/>--
                <input className={CSS(styles,"custom-time")} type={'time'} id={`${label}-end`+index} name={`${label}-end`+index} 
        defaultValue={end}/>
        </div>
    </>)
}

export function InputTextArea({label,value,index} : Props): JSX.Element{    
    return(<>
        <textarea id={label+index} name={label} rows={4} cols={50} defaultValue={value}></textarea>
    </>)
}

export function InputEmail({label,value,index} : Props): JSX.Element{
    return(<>
        <input type={'email'} id={label+index} name={label} 
        placeholder={`Enter a ${label.replace(/([a-z])([A-Z])/g, '$1 $2')}`} 
        defaultValue = {value || ''}
        />
    </>)
}//  z-index: 1;       

export function InputPassword({label,value,index} : Props): JSX.Element{
        const [showPassword, setShowPassword] = useState(false);
    return(<>
 <div className={CSS(styles,"password-container")}>
        <input className={CSS(styles,"custom-password")} type={showPassword?'text':'password'} id={label+index} name={label} 
        placeholder={`Enter a ${label.replace(/([a-z])([A-Z])/g, '$1 $2')}`} 
        defaultValue = {value || ''}
        />



        {showPassword ?  
        <svg xmlns="http://www.w3.org/2000/svg" 
        className={CSS(styles,"eye-icon")}
        onClick={() => setShowPassword(!showPassword)}
        version="1.1" id="Capa_1" x="0px" y="0px"
        viewBox="0 0 512.19 512.19">
        <g>
            <circle 
            cx="256.095" cy="256.095" r="85.333"/>
            <path 
            d="M496.543,201.034C463.455,147.146,388.191,56.735,256.095,56.735S48.735,147.146,15.647,201.034   c-20.862,33.743-20.862,76.379,0,110.123c33.088,53.888,108.352,144.299,240.448,144.299s207.36-90.411,240.448-144.299   C517.405,277.413,517.405,234.777,496.543,201.034z M256.095,384.095c-70.692,0-128-57.308-128-128s57.308-128,128-128  
            s128,57.308,128,128C384.024,326.758,326.758,384.024,256.095,384.095z"/>
        </g>
        </svg>
        :<svg className={CSS(styles,"eye-icon")}
        onClick={() => setShowPassword(!showPassword)}
                xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="512" height="512">
                <g id="_01_align_center" 
                data-name="01 align center">
                <path 
                d="M23.821,11.181v0a15.736,15.736,0,0,0-4.145-5.44l3.032-3.032L21.293,1.293,18,4.583A11.783,11.783,0,0,0,12,3C4.5,3,1.057,9.261.179,11.181a1.969,1.969,0,0,0,0,1.64,15.736,15.736,0,0,0,4.145,5.44L1.293,21.293l1.414,1.414L6,19.417A11.783,11.783,0,0,0,12,21c7.5,0,10.943-6.261,11.821-8.181A1.968,1.968,0,0,0,23.821,11.181ZM2,12.011C2.75,10.366,5.693,5,12,5a9.847,9.847,0,0,1,4.518,1.068L14.753,7.833a4.992,4.992,0,0,0-6.92,6.92L5.754,16.832A13.647,13.647,0,0,1,2,12.011ZM15,12a3,3,0,0,1-3,3,2.951,2.951,0,0,1-1.285-.3L14.7,10.715A2.951,2.951,0,0,1,15,12ZM9,12a3,3,0,0,1,3-3,2.951,2.951,0,0,1,1.285.3L9.3,13.285A2.951,2.951,0,0,1,9,12Zm3,7a9.847,9.847,0,0,1-4.518-1.068l1.765-1.765a4.992,4.992,0,0,0,6.92-6.92l2.078-2.078A13.584,13.584,0,0,1,22,12C21.236,13.657,18.292,19,12,19Z"/>
                </g>
                </svg>}

    </div>
    </>)
}
 
export function InputPhone({label,value,index} : Props): JSX.Element{
    return(<>
        <input type={'tel'} id={label+index} name={label} 
        placeholder={`Enter a ${label.replace(/([a-z])([A-Z])/g, '$1 $2')}`} 
        defaultValue = {value || ''}
        />
    </>)
}
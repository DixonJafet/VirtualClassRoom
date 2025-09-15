/* eslint-disable @typescript-eslint/no-unused-vars */
/* eslint-disable prefer-const */

import React from 'react'
import styles from './CSS/Table.module.css'
import { CSS } from '../utils/Functions'
import { Form } from 'react-router';


/* eslint-disable no-unused-vars */
interface Props{
    headers: Array<string>,
    data:object[],
    editData: (data:Record<string,string>,metaData:Record<string,string>,inputForm: FormData)=>Promise<void>,
    editHeaderFunc: (header: string)=>void,
    editableHd?:boolean,
    onClickCellFunc:(data:Record<string,string>)=>Promise<void>
}
function setInput(event: React.MouseEvent<HTMLElement>){
    const span: HTMLSpanElement = event.target as  HTMLSpanElement ;
    const table_cell: HTMLTableCellElement = span.closest('td') as HTMLTableCellElement;
    const input: HTMLInputElement = table_cell.querySelector('input') as HTMLInputElement;
    input.disabled = false;
    input.value = span.textContent as string

    input.focus();

 
}
function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>, saveFuction: (data: Record<string,string>,metaData: Record<string,string>,inputForm: FormData)=>void) {

    const input: HTMLInputElement =  document.activeElement  as HTMLInputElement;
    
    if (event.key === 'Enter' || event.key === 'insertTab') {
        event.preventDefault();
        event.stopPropagation();
        
        if (input.value !== '') {
            const table_cell: HTMLTableCellElement = input.closest('td') as HTMLTableCellElement;
            const span: HTMLSpanElement = table_cell.querySelector('span') as HTMLSpanElement;
            const table_row: HTMLTableRowElement = table_cell.closest('tr') as HTMLTableRowElement;
            let row: string  = table_row.id.replace('react-table-row-','') as string;
            let column: string = span.dataset.customValue as string;
            span.textContent = input.value;
            input.disabled = true;
            input.blur();
            span.focus();
            const inputForm = new FormData();
            inputForm.append("grade", input.value);
          //  inputForm.append("feedbackFile",'');
            saveFuction({ grade: input.value},{
                row,
                column
    
            }, inputForm);
        }
    }
}
function handleTdClick(event:  React.MouseEvent<HTMLTableCellElement>, actionFuction: (data: Record<string,string>)=>void){
    event.preventDefault();
    const table_cell: HTMLTableCellElement = event.target as HTMLTableCellElement
    const table_row: HTMLTableRowElement = table_cell.closest('tr') as HTMLTableRowElement;
    const span: HTMLSpanElement = table_cell.querySelector('span') as HTMLSpanElement;
    let row: string  = table_row.id.replace('react-table-row-','') as string;
    if (!span) return;
    let column: string | null = span.dataset.customValue as string;
    actionFuction({
        row,
        column})
}
export const Table: React.FC<Props> = ({
    headers,
    data,
    editData,
    editHeaderFunc,
    onClickCellFunc,
    editableHd
}) =>{
    return (

    <div className = {CSS(styles,'section')}>
    <table  className={CSS(styles,"table_data")} >

                <thead>
                <tr  key = {"rowHead"} className={CSS(styles,"row", "row-head")}>
                    {headers.map((header,index) =><th className={CSS(styles,"item")} key= {`react-table-header-${index}`} 
                        onClick={(event)=>editHeaderFunc(header)}>
                        {header} 
                        {editableHd?<svg xmlns="http://www.w3.org/2000/svg" 
                         viewBox="0 0 24 24" width="24px" height="24px">  
                        <path d="M 19.171875 2 C 18.448125 2 17.724375 2.275625 17.171875 2.828125 L 16 4 L 20 8 L 21.171875 6.828125 C 22.275875 5.724125 22.275875 3.933125 21.171875 2.828125 C 20.619375 2.275625 19.895625 2 19.171875 2 z M 14.5 5.5 L 3 17 L 3 21 L 7 21 L 18.5 9.5 L 14.5 5.5 z"/>
                        </svg>:<></>}
                        </th>)}
                </tr>
                </thead>
                <tbody>

                { data.map(( ObjectEntry, index)=>(
                    <tr key={`react-table-row-${index}`} id = {`react-table-row-${index}`} className={CSS(styles,"row")} > 
                    {Object.entries(ObjectEntry).map(([key, value])=>(
                        <td className={CSS(styles,"item")} key={`react-table-column-${key}`} onClick={(event)=>{handleTdClick(event,onClickCellFunc)}} >
                        <span 
                        onClick={(event)=>{setInput(event)}} data-custom-value = {`${key}`}>{value as string}
                        </span>
            
                        <input
                            onKeyDown = {(event)=>{ handleKeyDown(event,editData);
                            }}
                            type= "number"  
                            disabled = {true}  
                            id = {`${key}-input-`}
                            data-custom-value = {`${key}`}
                        />
                        </td>
                    ))}      
                    </tr>))}
                </tbody>
            </table></div>
    
      )
}
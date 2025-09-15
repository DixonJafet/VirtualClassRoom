/* eslint-disable @typescript-eslint/no-unused-vars */
/* eslint-disable prefer-const */
/* eslint-disable no-unused-vars */
import '../CSS/Form.css'
import React, { Children, ReactHTMLElement, ReactNode, useEffect, useState } from 'react'
import {schema, DynamicObject} from '../interfaces/interface.ts'

interface Props{
    handleSubmit?: (event: React.FormEvent<HTMLFormElement>)=>Promise<void>,
    formSchemas?:  schema[],
    children?: ReactNode
}
export const Prueba: React.FC<Props> = ({
    handleSubmit,
    formSchemas,
    children
}) =>{
   // console.log(formSchemas)
    //const pairedList = labels.map((element, index) => [element, values[index]]);
   
    
    formSchemas  = [
  {
      "label": "title",
      "value": "Proyect",
      "type": "text"
  },
  {
      "label": "value",
      "value": "20",
      "type": "number"
  },
  {
      "label": "subject",
      "value": "Data Analisys",
      "type": "text"
  },
  {
      "label": "statement",
      "value": "link/link",
      "type": "file"
  },
  {
      "label": "rubric",
      "value": "link/link",
      "type": "file"
  }
]
  formSchemas.map((schema:  schema)=>(console.log(schema.label)))
    
    return(
        <>    
        <form className="form-container">
            <div className="form-grid">
            {formSchemas.map((schema: schema)=>
              (
                <div className="form-group"  key = {schema.label}>
                  <label htmlFor={schema.label}>{schema.label}</label>
                  <input
                    type={schema.type}
                    id={schema.label}
                    name = {schema.label}
                    placeholder= { `Enter your ${schema.label}`}
                    
                    />
                </div>) )}
                <div className="form-group" >
                  {children}
                </div>
                </div>
          <button type="submit" className="submit-button">
            Submit
          </button>
        </form></>
    )
}
import React, { useEffect, useState } from 'react'
import {  EDITEvaluationInfo, GETAllGradesInfo ,EDITGradeInfo,
          DELEvaluation,NEWEvaluationInfo} from '../services/APIRequest.ts'
import {Table} from '../components/Table.tsx'
import {DynamicObject} from '../interfaces/interfaces.ts'
import { FloatFrame } from './FloatFrame.tsx';
import { useParams } from 'react-router'
import { TopSection } from '../components/TopSection.tsx';
import { Link } from 'react-router-dom';
import { GradeForm } from '../components/GradeForm.tsx';
import { EvaluationForm } from '../components/EvaluationForm.tsx';
import { Grade } from '../interfaces/interfaces.ts';

export function EvaluationPage() {
  const [cardData,setCardData] = useState([{}])
  const [headers,setHeaders] = useState([""])
  const [float,setFloat] = useState(<></>)
  const {ClassRoomName,classroomCode} = useParams()

  useEffect(()=>{
    updateCardData([{}])
  },[]);


async function handleNewEvaluation(formData: FormData): Promise<void>{
  const data: DynamicObject = {};
  formData.forEach((value, key: string) => {data[key] = value as string});
  console.log(data.title);
  const newCardData: Record<string, string | number>[] = cardData.map((student: Record<string, string | number>) => {
    const newInfo: Record<string, string | number> = { ...student };
    newInfo[data.title as string] = 0;
    return newInfo;
  });
  updateCardData(newCardData);
  setFloat(<></>);
  NEWEvaluationInfo(formData,classroomCode as string)
}

async function handleEditEvaluation(formData:FormData): Promise<void>{
  const prevName = formData.get('prevName') as string;
  const data: DynamicObject = {};
  formData.forEach((value, key: string) => {if (key!= "prevName"){data[key] = value as string}});
  const newCardData: Record<string, string | number>[] = cardData.map((student: Record<string, string | number>) => {
    const newInfo: Record<string, string | number> = { ...student };
    const prevValue: string | number | Date = newInfo[prevName];
    if (prevName in newInfo) {
      delete newInfo[prevName];
    }
    newInfo[data.title as string] = prevValue;
    return newInfo;
  });
  updateCardData(newCardData);
  setFloat(<></>);
  EDITEvaluationInfo(formData,classroomCode as string,prevName);
}


async function updateCardData(data: Record<string, string | number>[]): Promise<void> {
  if (data.length === 1 && Object.keys(data[0]).length === 0) {
    const rawData = await GETAllGradesInfo(classroomCode as string);
    data = rawData.map((item: DynamicObject) => {
      const filtered: Record<string, string | number> = {};
      Object.entries(item).forEach(([key, value]) => {
        if (typeof value === "string" || typeof value === "number") {
          filtered[key] = value;
        }
      });
      return filtered;
    });
  }
  console.log("Data received:", data);
  // Add "total" attribute to each element
  const updatedData = data.map((item: Record<string, string | number>) => {
    let total = 0;
    delete item.total;
    Object.entries(item).forEach(([key, value]) => {
      // Only sum numeric values and skip "Name" or non-numeric fields
      if (key.toLowerCase() !== "name" && typeof value === "number") {
        total += value;
      } else if (key.toLowerCase() !== "name" && typeof value === "string" && !isNaN(Number(value))) {
        total += Number(value);
      }
    });
    return { ...item, total };
  });

  let Headers: string[] = Object.keys(updatedData[0]);
  Headers = Headers.map(Header => Header.replaceAll('_', ' '));
  Headers = Headers.map(Header => Header.charAt(0).toUpperCase() + Header.slice(1));
  setHeaders(Headers);
  setCardData(updatedData);
}

function deleteEvaluation(evaluation: string){
    //const newHeaders: string[] = headers.filter((header: string) => header !== evaluation);
    const newData: Record<string, string | number>[] = cardData.map((info: Record<string, string | number>) => {
      const newInfo: Record<string, string | number> = { ...info };
      delete newInfo[evaluation];
      return newInfo;
    });
    DELEvaluation(classroomCode as string, evaluation)
    updateCardData(newData);
    setFloat(<></>)
}
async function newEvaluation(){
  renderEvaluationForm(null)
}

async function renderEvaluationForm(name: string | null): Promise<void> {
      setFloat(<>
      <FloatFrame  closeFunction = {clean_frame}>
      <EvaluationForm classroomCode={classroomCode as string} name={name} 
      handleEditEvaluation={handleEditEvaluation} handleNewEvaluation={handleNewEvaluation} 
      deleteEvaluation={deleteEvaluation}
      clean_frame = {clean_frame} />
      </FloatFrame></>)
}

async function editData(data: DynamicObject ,metadata: DynamicObject,formData: FormData): Promise<void>{
  const allCardData = cardData
  const toEdit: DynamicObject = allCardData[Number(metadata['row'])]
  console.log(metadata["column"] );
   console.log(data['grade'] );
  toEdit[metadata["column"] as string] = data['grade'];
  const name: string = metadata['name'] as string || toEdit['Name'] as string;
  EDITGradeInfo(formData,classroomCode as string,name,metadata['column'] as string);
  updateCardData(allCardData)
}

async function renderGradeForm(position:Record<string, string>):Promise<void>{
  console.log(position)
  const numberRow = Number(position.row);
  const rowObject: Grade = cardData[numberRow] as Grade;
  const name: string = rowObject['Name'] as string;
  const evaluation = position.column
  console.log(name, evaluation,rowObject);
      setFloat(<>
      <FloatFrame  closeFunction = {clean_frame}>
      <GradeForm classRoomCode={classroomCode as string} name={name} evaluation={evaluation}
      editData={editData} row={position.row} column={position.column}
      clean_frame = {clean_frame} />
      </FloatFrame></>)
}


function clean_frame(){
  setFloat(<></>)
}

 return (
  <>
    <TopSection>
      <div>
        <h1><Link to='/Home/ClassRooms'>{'Classroom > '}</Link></h1>
        <h1><Link to={`/Home/ClassRooms/${classroomCode}/${ClassRoomName}`}>{ClassRoomName}</Link></h1>
      </div>
      <button onClick={newEvaluation}>+ Evaluation</button>
    </TopSection>
    <Table headers = {headers} data= {cardData} editData = {editData} 
      editableHd editHeaderFunc={renderEvaluationForm} onClickCellFunc={renderGradeForm}>
    </Table>
    {float}
  </>
)
}
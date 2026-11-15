import React, { useEffect, useState } from 'react'
import {  EDITEvaluationInfo, GETAllGradesInfo, GETClassRoomEvaluationMaxima, EDITGradeInfo,
          DELEvaluation,NEWEvaluationInfo} from '../services/APIRequest.ts'
import {Table} from '../components/Table.tsx'
import {DynamicObject} from '../interfaces/interfaces.ts'
import { FloatFrame } from './FloatFrame.tsx';
import { useParams, Link } from 'react-router-dom'
import { TopSection } from '../components/TopSection.tsx';
import { GradeForm } from '../components/GradeForm.tsx';
import { EvaluationForm } from '../components/EvaluationForm.tsx';
import { Grade } from '../interfaces/interfaces.ts';
import { CSS } from '../utils/Functions.ts';
import styles from '../components/CSS/TopSection.module.css';
import tableStyles from '../components/CSS/Table.module.css';
import { courseTotal, validGrade } from '../utils/percentages';

export function EvaluationPage() {
  const [cardData,setCardData] = useState([{}])
  const [headers,setHeaders] = useState([""])
  const [evaluationMaxima,setEvaluationMaxima] = useState<Record<string, number>>({})
  const [float,setFloat] = useState(<></>)
  const {ClassRoomName,classroomCode} = useParams()

  useEffect(()=>{
    let cancelled = false;

    async function loadClassroomData(): Promise<void> {
      const [rawData, maxima] = await Promise.all([
        GETAllGradesInfo(classroomCode as string),
        GETClassRoomEvaluationMaxima(classroomCode as string),
      ]);
      if (cancelled) return;

      const rows = rawData.map((item: DynamicObject) => {
        const filtered: Record<string, string | number> = {};
        Object.entries(item).forEach(([key, value]) => {
          if (typeof value === "string" || typeof value === "number") {
            filtered[key] = value;
          }
        });
        return filtered;
      });

      setEvaluationMaxima(maxima ?? {});
      if (maxima === null) console.error('Could not load evaluation values.');
      updateCardData(rows);
    }

    void loadClassroomData();
    return () => {
      cancelled = true;
    };
  },[classroomCode]);


async function handleNewEvaluation(formData: FormData): Promise<void>{
  const result = await NEWEvaluationInfo(formData, classroomCode as string);
  if (result !== 'success') {
    window.alert('Could not create evaluation. Check that the values total at most 100%.');
    return;
  }
  const data: DynamicObject = {};
  formData.forEach((value, key: string) => {data[key] = value as string});
  const newCardData: Record<string, string | number>[] = cardData.map((student: Record<string, string | number>) => {
    const newInfo: Record<string, string | number> = { ...student };
    newInfo[data.title as string] = 0;
    return newInfo;
  });
  const title = data.title as string;
  const weight = Number(data.value);
  if (title && Number.isFinite(weight)) {
    setEvaluationMaxima(previous => ({ ...previous, [title]: weight }));
  }
  updateCardData(newCardData);
  setFloat(<></>);
}

async function handleEditEvaluation(formData:FormData): Promise<void>{
  const prevName = formData.get('prevName') as string;
  const result = await EDITEvaluationInfo(formData, classroomCode as string, prevName);
  if (result !== 'success') {
    window.alert('Could not update evaluation. Check its value and existing grades.');
    return;
  }
  const data: DynamicObject = {};
  formData.forEach((value, key: string) => {if (key!= "prevName"){data[key] = value as string}});
  const title = data.title as string;
  const weight = Number(data.value);
  if (title && Number.isFinite(weight)) {
    setEvaluationMaxima(previous => {
      const updated = { ...previous };
      delete updated[prevName];
      updated[title] = weight;
      return updated;
    });
  }
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
}


function updateCardData(data: Record<string, string | number>[]): void {
  const updatedData: Record<string, string | number>[] = data.map((item: Record<string, string | number>) => {
    return { ...item, total: courseTotal(item) };
  });
  updatedData.sort((left, right) =>
    String(left['Name'] ?? '').localeCompare(String(right['Name'] ?? ''), undefined, { sensitivity: 'base' })
  );

  const Headers: string[] = Object.keys(updatedData[0] ?? {});
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
    setEvaluationMaxima(previous => {
      const updated = { ...previous };
      delete updated[evaluation];
      return updated;
    });
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
  const allCardData = cardData.map(row => ({ ...row }));
  const toEdit: DynamicObject = allCardData[Number(metadata['row'])];
  const name: string = metadata['name'] as string || toEdit['Name'] as string;
  const result = await EDITGradeInfo(formData,classroomCode as string,name,metadata['column'] as string);
  if (result !== 'success') {
    window.alert('Could not save grade. Check the score and try again.');
    return;
  }
  toEdit[metadata["column"] as string] = data['grade'];
  updateCardData(allCardData)
}

async function handleTableEdit(
  data: Record<string, string>,
  metadata: Record<string, string>,
  formData: FormData
): Promise<void> {
  const maximum = evaluationMaxima[metadata.column];
  const percentage = Number(data.grade);
  if (maximum === undefined || !validGrade(percentage, maximum)) {
    window.alert(maximum === undefined ? 'Evaluation value is unavailable.' : `Enter a percentage between 0 and ${maximum}.`);
    return;
  }
  await editData(data, metadata, formData);
}

async function renderGradeForm(position:Record<string, string>):Promise<void>{
  const numberRow = Number(position.row);
  const rowObject: Grade = cardData[numberRow] as Grade;
  const name: string = rowObject['Name'] as string;
  const evaluation = position.column;
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

function formatCell(value: unknown, key: string): string {
  if (key.toLowerCase() === 'name') return String(value);
  if (typeof value !== 'number' && typeof value !== 'string') return String(value);
  const numericValue = Number(value);
  if (!Number.isFinite(numericValue)) return String(value);
  const formatted = String(Number(numericValue.toFixed(2)));
  return `${formatted}%`;
}

function getCellClassName(value: unknown, key: string): string | undefined {
  const column = key.toLowerCase();
  if (column !== 'total') return undefined;

  const percentage = Number(value);

  if (Number.isFinite(percentage) && percentage < 68) {
    return tableStyles['low-score'];
  }
}

 return (
  <>
    <TopSection>
      <div>
        <h1><Link to='/Home/ClassRooms'>{'Classroom > '}</Link></h1>
        <h1><Link to={`/Home/ClassRooms/${classroomCode}/${ClassRoomName}`}>{ClassRoomName}</Link></h1>
      </div>
      <div className={CSS(styles, 'button-group')}>
        <button type="button" onClick={newEvaluation}>+ Evaluation</button>
      </div>
    </TopSection>
    <Table headers = {headers} data={cardData} editData={handleTableEdit}
      formatCell={formatCell} formatHeader={header => evaluationMaxima[header] === undefined ? header : `${header}: ${evaluationMaxima[header]}%`} getCellClassName={getCellClassName}
      editableHd editHeaderFunc={renderEvaluationForm} onClickCellFunc={renderGradeForm}>
    </Table>
    {float}
  </>
)
}
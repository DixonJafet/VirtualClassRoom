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

export function EvaluationPage() {
  const [cardData,setCardData] = useState([{}])
  const [headers,setHeaders] = useState([""])
  const [evaluationMaxima,setEvaluationMaxima] = useState<Record<string, number>>({})
  const [percentageError,setPercentageError] = useState(false)
  const [isLoading,setIsLoading] = useState(true)
  const [displayMode,setDisplayMode] = useState<'pts' | 'percent'>('pts')
  const [float,setFloat] = useState(<></>)
  const {ClassRoomName,classroomCode} = useParams()

  useEffect(()=>{
    let cancelled = false;

    async function loadClassroomData(): Promise<void> {
      const rawData = await GETAllGradesInfo(classroomCode as string);
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

      const evaluationTitles = Object.keys(rows[0] ?? {}).filter(
        key => key.toLowerCase() !== 'name' && key.toLowerCase() !== 'total'
      );
      const maxima = await GETClassRoomEvaluationMaxima(classroomCode as string);
      if (cancelled) return;

      setEvaluationMaxima(maxima ?? {});
      setPercentageError(maxima === null || evaluationTitles.some(title => maxima[title] === undefined));
      updateCardData(rows);
      setIsLoading(false);
    }

    void loadClassroomData();
    return () => {
      cancelled = true;
    };
  },[classroomCode]);


async function handleNewEvaluation(formData: FormData): Promise<void>{
  const data: DynamicObject = {};
  formData.forEach((value, key: string) => {data[key] = value as string});
  const newCardData: Record<string, string | number>[] = cardData.map((student: Record<string, string | number>) => {
    const newInfo: Record<string, string | number> = { ...student };
    newInfo[data.title as string] = 0;
    return newInfo;
  });
  const title = data.title as string;
  const maxPoints = Number(data.value);
  if (title && Number.isFinite(maxPoints) && maxPoints > 0) {
    setEvaluationMaxima(previous => ({ ...previous, [title]: maxPoints }));
  }
  updateCardData(newCardData);
  setFloat(<></>);
  NEWEvaluationInfo(formData,classroomCode as string)
}

async function handleEditEvaluation(formData:FormData): Promise<void>{
  const prevName = formData.get('prevName') as string;
  const data: DynamicObject = {};
  formData.forEach((value, key: string) => {if (key!= "prevName"){data[key] = value as string}});
  const title = data.title as string;
  const maxPoints = Number(data.value);
  if (title && Number.isFinite(maxPoints) && maxPoints > 0) {
    setEvaluationMaxima(previous => {
      const updated = { ...previous };
      delete updated[prevName];
      updated[title] = maxPoints;
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
  EDITEvaluationInfo(formData,classroomCode as string,prevName);
}


function updateCardData(data: Record<string, string | number>[]): void {
  // Add "total" attribute to each element
  const updatedData = data.map((item: Record<string, string | number>) => {
    let total = 0;
    Object.entries(item).forEach(([key, value]) => {
      // Only sum numeric values and skip "Name" or non-numeric fields
      if (key.toLowerCase() !== "name" && key.toLowerCase() !== "total" && typeof value === "number") {
        total += value;
      } else if (key.toLowerCase() !== "name" && key.toLowerCase() !== "total" &&
                 typeof value === "string" && !isNaN(Number(value))) {
        total += Number(value);
      }
    });
    return { ...item, total: Number(total.toFixed(2)) };
  });

  let Headers: string[] = Object.keys(updatedData[0] ?? {});
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
  const allCardData = cardData
  const toEdit: DynamicObject = allCardData[Number(metadata['row'])]
  toEdit[metadata["column"] as string] = data['grade'];
  const name: string = metadata['name'] as string || toEdit['Name'] as string;
  EDITGradeInfo(formData,classroomCode as string,name,metadata['column'] as string);
  updateCardData(allCardData)
}

async function handleTableEdit(
  data: Record<string, string>,
  metadata: Record<string, string>,
  formData: FormData
): Promise<void> {
  const maxPoints = evaluationMaxima[metadata.column];
  if (displayMode === 'percent' && maxPoints) {
    const points = Number(data.grade) * maxPoints / 100;
    data.grade = String(points);
    formData.set('grade', String(points));
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

const displayedData = cardData.map((row: Record<string, string | number>) => {
  if (displayMode === 'pts') return row;

  const totalPossible = Object.values(evaluationMaxima).reduce((sum, value) => sum + value, 0);
  const maximaComplete = Object.keys(row).every(key =>
    key.toLowerCase() === 'name' || key.toLowerCase() === 'total' || evaluationMaxima[key] !== undefined
  );
  return Object.fromEntries(Object.entries(row).map(([key, value]) => {
    if (key.toLowerCase() === 'name') return [key, value];
    if (key.toLowerCase() === 'total') {
      return [key, maximaComplete && totalPossible > 0 ? Number(value) / totalPossible * 100 : Number.NaN];
    }
    const maxPoints = evaluationMaxima[key];
    return [key, maxPoints ? Number(value) / maxPoints * 100 : Number.NaN];
  }));
});

function formatCell(value: unknown, key: string): string {
  if (typeof value !== 'number' && typeof value !== 'string') return String(value);
  const numericValue = Number(value);
  if (!Number.isFinite(numericValue)) return String(value);
  const formatted = String(Number(numericValue.toFixed(2)));
  return displayMode === 'percent' && key.toLowerCase() !== 'name'
    ? `${formatted}%`
    : key.toLowerCase() === 'total' ? formatted : String(value);
}

const percentageUnavailable = isLoading || Object.keys(cardData[0] ?? {}).some(key =>
  key.toLowerCase() !== 'name' && key.toLowerCase() !== 'total' && evaluationMaxima[key] === undefined
);

 return (
  <>
    <TopSection>
      <div>
        <h1><Link to='/Home/ClassRooms'>{'Classroom > '}</Link></h1>
        <h1><Link to={`/Home/ClassRooms/${classroomCode}/${ClassRoomName}`}>{ClassRoomName}</Link></h1>
      </div>
      <div className={CSS(styles, 'button-group')}>
        <button
          type="button"
          onClick={() => setDisplayMode(mode => mode === 'pts' ? 'percent' : 'pts')}
          disabled={percentageUnavailable || percentageError}
          aria-pressed={displayMode === 'percent'}
          title={percentageUnavailable || percentageError ? 'Percentage view is unavailable because an evaluation maximum could not be loaded.' : undefined}
        >
          {displayMode === 'pts' ? 'Show %' : 'Show pts'}
        </button>
        <button type="button" onClick={newEvaluation}>+ Evaluation</button>
      </div>
    </TopSection>
    <Table headers = {headers} data={displayedData} editData={handleTableEdit}
      formatCell={formatCell}
      editableHd editHeaderFunc={renderEvaluationForm} onClickCellFunc={renderGradeForm}>
    </Table>
    {float}
  </>
)
}
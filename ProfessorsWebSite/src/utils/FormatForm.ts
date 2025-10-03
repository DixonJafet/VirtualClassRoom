/* eslint-disable prefer-const */
import {schema, Grade} from '../interfaces/interfaces.ts'
import { GETClassRoomsInfo, GETEvaluationInfo,GETGradeInfo,GETProfileInfo} from '../services/APIRequest.ts'

const evaluationFormSchema: Record<string,string> = {
  title:"text",
  value:"number",
  dueDate:"date",
  subject:"text",
  statement:"file",
  rubric:"file",
  prevName :"hidden"

}
const gradeFormSchema:Record<string,string> ={
  grade:"number",
  feedback:"file",
  max:"hidden"
}
const classroomFormSchema:Record<string,string> = {
  courseName:"text",
  about:"file",
  classroomCode:"hidden",
}

const daysFormSchema:Record<string,string> = {
  day:"days",
  time:"time"
}

const profileFormSchema:Record<string,string> = {
  name:"text",
  phone:"number",
  area_of_expertise:"text",
  email:"email",
  password:"password",
}

const loginFormatSchema:Record<string,string> = {
  email:"email",
  password:"password",
}

export async function loginFormFormat(email: string | null, password: string | null): Promise<schema[]> {
  const loginInfo = (email && password)?{email, password}:null
  return returnFormSchema(loginInfo, loginFormatSchema)
}

export async function profileFormFormat(name: string | null): Promise<schema[]> {
  const profileInfo = (name)?await GETProfileInfo(name):null;
  return returnFormSchema(profileInfo, profileFormSchema);

}

export async function classroomFormFormat(classnumber: string | null): Promise<schema[]> {
  let classroomInfo = (classnumber)?await GETClassRoomsInfo(classnumber):null
  const days = classroomInfo?.schedule
  const { schedule, ...classroomData } = classroomInfo || {}
  let classroomDataFormated = classroomData as Record<string,string | number>
  const day_time_list = days?.map((day: any)=>(returnFormSchema(day,daysFormSchema))) || []
  const classroomInfoFormated = returnFormSchema(classroomDataFormated,classroomFormSchema)
  return [...classroomInfoFormated, ...day_time_list.flat()]
}

export async function evaluationFormFormat(classroomCode: string ,name: string | null ): Promise<schema[]> {
  let evaluationInfo = (name)?await GETEvaluationInfo(classroomCode,name):null
  if (evaluationInfo) {
    evaluationInfo["prevName"] = name || "";
  }
  // Map File and undefined values to empty string
  const evaluationInfoFormatted = evaluationInfo
    ? Object.fromEntries(
        Object.entries(evaluationInfo).map(([key, value]) => [
          key,
          value instanceof File || value === undefined ? "" : value
        ])
      )
    : null;
  return returnFormSchema(evaluationInfoFormatted, evaluationFormSchema)
}

export async function gradeFormFormat(classRoomCode: string, name: string | undefined, evaluation: string | null): Promise<schema[]> {
  const gradeInfo: Grade | null = (name) ? await GETGradeInfo(classRoomCode,name, evaluation) : null;
  // Map File and undefined values to empty string
  const gradeInfoFormatted = gradeInfo
    ? Object.fromEntries(
        Object.entries(gradeInfo).map(([key, value]) => [
          key,
          value instanceof File || value === undefined ? "" : value
        ])
      )
    : null;
  return returnFormSchema(gradeInfoFormatted, gradeFormSchema);
}

export  function returnFormSchema(Info: Record<string,string | number> | null,
                                        FormSchema: Record<string,string>
): schema[]{
  const  labels: string[] = Object.keys(FormSchema);
  const  values: (string | number)[] = (Info)?labels.map((label: string)=>(Info[label])):labels.map(()=>(""))
  const  types: string[] = labels.map((label)=>FormSchema[label])
  const form_schema: schema[] = [];
  let new_input: schema;
  
  for(let i = 0 ; i<labels.length; i++){
    new_input = {
      label: labels[i],
      value: values[i],
      type: types[i]
    }
    form_schema.push(new_input)
  }
  return form_schema
}
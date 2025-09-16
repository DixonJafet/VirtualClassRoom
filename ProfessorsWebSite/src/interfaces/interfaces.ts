export interface schema{
    
  label: string,
  value?: string | number,
  type: string
 
}

export interface DynamicObject {
  [key: string]: string | number | File | undefined;
}



export interface Evaluation {
  title: string;
  value: number;
  dueDate: string ;
  subject: string;
  statement: string | File;
  statementFile?: File;
  rubric: string | File;
  rubricFile?: File;
  [key: string]: string | number | File | undefined;
}

export interface Schedule {

  day: string;
  time: string;

}

export interface ClassRoom {
  classroomCode: string;
  courseName: string;
  about: string | File;
  aboutFile?: File;
  schedule: Schedule[];
  [key: string]: unknown;
}

export interface Grade {
  grade: number;
  max:number;
  feedback: string | File;
  feedbackFile: File;
  [key: string]: string | number | File;
}


export interface professor {
  name: string;
  phone: number;
  area_of_expertise: string;
  email: string;
  password: string;
  [key: string]: string | number;
}
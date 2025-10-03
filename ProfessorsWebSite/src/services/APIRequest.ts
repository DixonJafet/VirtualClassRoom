import {
  DynamicObject,
  Evaluation,
  Grade,
  ClassRoom,
  professor
} from "../interfaces/interfaces";


export async function NEWEvaluationInfo(data: FormData, classroomCode: string): Promise<string> {
  

  try {
    const response = await fetch(`https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/createEvaluation/${encodeURIComponent(classroomCode)}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
      body: data,
    });

    if (!response.ok) throw new Error('Failed to create evaluation');
    const result = await response.json();
    return "success";
  } catch (error) {
    return "error";
  }
}


export async function SETClassRoom(classroom: FormData): Promise<string> {

  try {
    const response = await fetch('https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/newClassRoom', {
      method: 'POST',
      credentials: "include",
      body: classroom
    });

    if (!response.ok) throw new Error('Failed to create classroom');
    const data = await response.json();
    return data.classroomCode || "success";
  } catch (error) {
    return "error";
  }
}

export async function GETAllGradesInfo(classroomCode: string): Promise<DynamicObject[]> {
  //return( mokData);

    try {
    const response = await fetch(`https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/ClassRoomEvaluation/${classroomCode}`, {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
    });

    if (!response.ok) throw new Error('Failed to fetch grades');
    const data = await response.json();
    //  
    // Assuming the API returns an array of StudentData
    return data || [];
  } catch (error) {
    return [];
  }
}


export async function DELEvaluation(ClassroomCode: string,evaluation_title: string): Promise<void> {

  try {
    const response = await fetch(`https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/deleteEvaluation/${encodeURIComponent(ClassroomCode)}/${encodeURIComponent(evaluation_title)}`, {
      method: 'DELETE',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include"
    });

    if (!response.ok) throw new Error('Failed to delete evaluation');
    await response.json();
  } catch (error) {
  }

}


export async function GETEvaluationInfo(classRoomCode: string, evaluationTitle: string ): Promise<Evaluation | null> {
  try {
    const response = await fetch(
      `https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/getEvaluation/${encodeURIComponent(classRoomCode)}/${encodeURIComponent(evaluationTitle)}`,
      {
        method: 'GET',
        headers: { 'Content-Type': 'application/json' },
        credentials: "include",
      }
    );

    if (!response.ok) throw new Error('Failed to fetch evaluation info');
    const data = await response.json();
    return data as Evaluation;
  } catch (error) {
    return null;
  }
}

export async function EDITEvaluationInfo(data: FormData ,classRoomCode: string,evaluationTitle: string): Promise<string> {
 
  try {
    const response = await fetch(`https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/editEvaluation/${encodeURIComponent(classRoomCode)}/${encodeURIComponent(evaluationTitle)}`, {
      method: 'PUT',
      credentials: "include",
      body: data,
    });

    if (!response.ok) throw new Error('Failed to edit evaluation');
    await response.json();
    return "success";
  } catch (error) {
    return "error";
  }
}

export async function GETGradeInfo(classCode: string, student_name: string, evaluation_name: string | null): Promise<Grade | null> {
  try {
    const response = await fetch(
      `https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/grade/${encodeURIComponent(classCode)}/${encodeURIComponent(student_name)}/${encodeURIComponent(evaluation_name ?? "")}`,
      {
        method: 'GET',
        headers: { 'Content-Type': 'application/json' },
        credentials: "include",
      }
    );

    if (!response.ok) throw new Error('Failed to fetch grade info');
    const data = await response.json();
    return data as Grade;
  } catch (error) {
    console.error('Error fetching grade info:', error);
    return null;
  }
}

export async function EDITGradeInfo(
  data: FormData,
  classRoomCode: string,
  student_name: string,
  evaluation_name: string | null
): Promise<string> {
  try {
    const response = await fetch(
      `https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/editGrade/${encodeURIComponent(classRoomCode)}/${encodeURIComponent(student_name)}/${encodeURIComponent(evaluation_name ?? "")}`,
      {
        method: 'PUT',
        credentials: "include",
        body: data,
      }
    );

    if (!response.ok) throw new Error('Failed to edit grade');
    await response.json();
    return "success";
  } catch (error) {
    console.error('Error editing grade:', error);
    return "error";
  }
}




export async function GETAllClassRooms(professor: string): Promise<ClassRoom[]> {
    let data;
  try {
    const response = await fetch('https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/ClassRooms',{
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
    });

    if (!response.ok) throw new Error('Failed to fetch');
    data = await response.json();
  } catch (err) {
  }
  // Assuming data is an array of ClassRoom objects
  const classrooms:ClassRoom[] = data['classRooms'] || [];
  return classrooms;
  
}

export async function EDITClassRooms(classroom: FormData):Promise<string>{
  try {
    const response = await fetch(`https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/edit/${classroom.get("classroomCode")}`, {
      method: 'PUT',
      credentials: "include",
      body:classroom,
    });
    if (!response.ok) throw new Error('Failed to edit classroom');
    await response.json();
  } catch (error) {
    return "error";
  }   
  
  return "success";
}

export async function GETClassRoomsInfo(classroomCode: string): Promise<ClassRoom | null> {
  try{
    const response = await fetch(`https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/edit/${classroomCode}`, {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
    });
    
    if (!response.ok) throw new Error('Failed to fetch classroom info');
    
    const data = await response.json();

    
    return data as ClassRoom;
  }catch (error) {
    console.error('Error fetching classroom info:', error);
  }
  // If the fetch fails or the classroom is not found, return null
  console.error(`Classroom with code ${classroomCode} not found`);

  
  return null;
}

export async function DELETEClassRoom(classroomCode: string): Promise<string> {
   try {
    const response = await fetch(`https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/delete/${classroomCode}`, {
      method: 'DELETE',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include"
    });

    if (!response.ok) {
      throw new Error('Failed to delete user');
      return "error";
    }

    await response.json(); // or .text(), depending on API
    return "success";
  } catch (err) {
    console.error(err);
    return "error";
  }
}
export async function LogInAction(data: { email: string; password: string }): Promise<boolean> {

  try{
    const response = await fetch('https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/LogIn', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
      body: JSON.stringify(data),
    });
    if (!response.status.toString().startsWith('2')) {
      throw new Error('Network response was not ok');
  
    }
    const result = await response.json();

    return true;
  } catch (error) {
    console.error('Error:', error);     
    return false;
  }

}
export async function SingUPaction(data: professor): Promise<boolean> {  
  try {
    const response = await fetch('https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/SingUp', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
      body: JSON.stringify(data),
    });
    if (!response.status.toString().startsWith('2')) {
      throw new Error('Network response was not ok');
  
    }
    const result = await response.json();
    console.log('Profesor:', result);
    return true;
  } catch (error) {
    console.error('Error:', error);     
    return false;
  }

}
export async function GETProfileInfo(name: string): Promise<professor | null> {
  try {
    const response = await fetch(`https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/Profile`, {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
    });

    if (!response.ok) throw new Error('Failed to fetch profile info');
    const data = await response.json();
    return data;
  } catch (error) {
    console.error('Error fetching profile info:', error);
    return null;
  }
}

export async function updateProfileInfo(data: Record<string, string | number>): Promise<void> {
  try {
    const response = await fetch('https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/UpdateProfileInfo', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
      body: JSON.stringify(data),
    });

    if (!response.ok) throw new Error('Failed to update profile');
    await response.json();
  } catch (error) {
    console.error('Error updating profile:', error);
  }
}

export async function LogOutAction(): Promise<boolean> {
  try {
    const response = await fetch('https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/LogOut', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
    });

    if (!response.ok) throw new Error('Failed to log out');
    return true;
  } catch (error) {
    console.error('Error logging out:', error);

  }

  return false;
}



export async function GETAboutFile(classroomCode: string ): Promise<Blob> {
        try{
        const response = await fetch(`https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/aboutFile/${classroomCode}`, {
          method: 'GET',
          credentials: "include",
        });
        
        if (!response.ok) throw new Error('Failed to fetch classroom info');
        const blob = await response.blob();
        return blob;
      }catch (error) {
        console.error('Error fetching classroom info:', error);
      }
      // If the fetch fails or the classroom is not found, return null
      console.error(`Classroom with code ${classroomCode} not found`);
      return null as unknown as Blob;

}


export async function GETrubricFile(classRoomCode: string, evaluationTitle: string ):  Promise<Blob> {
  try {
    const response = await fetch(
      `https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/rubricFile/${encodeURIComponent(classRoomCode)}/${encodeURIComponent(evaluationTitle)}`,
      {       
          method: 'GET',
          credentials: "include",
        });

      if (!response.ok) throw new Error('Failed to fetch classroom info');
          const blob = await response.blob();
          return blob;
      }catch (error) {
        console.error('Error fetching classroom info:', error);
      }
      // If the fetch fails or the classroom is not found, return null
      console.error(`Classroom with code ${classRoomCode} not found`);
      return null as unknown as Blob;
}


export async function GETstatementFile(classRoomCode: string, evaluationTitle: string ):  Promise<Blob> {
  try {
    const response = await fetch(
      `https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/statementFile/${encodeURIComponent(classRoomCode)}/${encodeURIComponent(evaluationTitle)}`,
      {       
          method: 'GET',
          credentials: "include",
        });

      if (!response.ok) throw new Error('Failed to fetch classroom info');
          const blob = await response.blob();
          return blob;
      }catch (error) {
        console.error('Error fetching classroom info:', error);
      }
      // If the fetch fails or the classroom is not found, return null
      console.error(`Classroom with code ${classRoomCode} not found`);
      return null as unknown as Blob;
}

export async function GETFeedBakcFile(classroomCode: string, student_name:string, evaluation_name:string): Promise<Blob> {
        try{
        const response = await fetch(`https://professorapi-fxebbwdnhqcbc5au.mexicocentral-01.azurewebsites.net/api/profes/feedBackFile/${encodeURIComponent(classroomCode)}/${encodeURIComponent(student_name)}/${encodeURIComponent(evaluation_name ?? "")}`, {
          method: 'GET',
          credentials: "include",
        });
        
        if (!response.ok) throw new Error('Failed to fetch classroom info');
        
        const blob = await response.blob();

   
        return blob;
      }catch (error) {
        console.error('Error fetching classroom info:', error);
      }
      // If the fetch fails or the classroom is not found, return null
      console.error(`Classroom with code ${classroomCode} not found`);
      return null as unknown as Blob;

}


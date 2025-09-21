import {
  DynamicObject,
  Evaluation,
  Grade,
  ClassRoom,
  professor
} from "../interfaces/interfaces";


export async function NEWEvaluationInfo(data: FormData, classroomCode: string): Promise<string> {
  

  try {
    const response = await fetch(`https://localhost:7273/api/profes/createEvaluation/${encodeURIComponent(classroomCode)}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
      body: data,
    });

    if (!response.ok) throw new Error('Failed to create evaluation');
    const result = await response.json();
    console.log('Created Evaluation:', result);
    return "success";
  } catch (error) {
    console.error('Error creating evaluation:', error);
    return "error";
  }
}


export async function SETClassRoom(classroom: FormData): Promise<string> {

  try {
    const response = await fetch('https://localhost:7273/api/profes/newClassRoom', {
      method: 'POST',
      credentials: "include",
      body: classroom
    });

    if (!response.ok) throw new Error('Failed to create classroom');
    const data = await response.json();
    console.log('Created Classroom:', data);
    return data.classroomCode || "success";
  } catch (error) {
    console.error('Error creating classroom:', error);
    return "error";
  }
}

export async function GETAllGradesInfo(classroomCode: string): Promise<DynamicObject[]> {
  //return( mokData);

    try {
    const response = await fetch(`https://localhost:7273/api/profes/ClassRoomEvaluation/${classroomCode}`, {
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
    console.error('Error fetching grades:', error);
    return [];
  }
}


export async function DELEvaluation(ClassroomCode: string,evaluation_title: string): Promise<void> {

  try {
    const response = await fetch(`https://localhost:7273/api/profes/deleteEvaluation/${encodeURIComponent(ClassroomCode)}/${encodeURIComponent(evaluation_title)}`, {
      method: 'DELETE',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include"
    });

    if (!response.ok) throw new Error('Failed to delete evaluation');
    const result = await response.json();
    console.log('Evaluation deleted:', result);
  } catch (error) {
    console.error('Error deleting evaluation:', error);
  }

}


export async function GETEvaluationInfo(classRoomCode: string, evaluationTitle: string ): Promise<Evaluation | null> {
  try {
    const response = await fetch(
      `https://localhost:7273/api/profes/getEvaluation/${encodeURIComponent(classRoomCode)}/${encodeURIComponent(evaluationTitle)}`,
      {
        method: 'GET',
        headers: { 'Content-Type': 'application/json' },
        credentials: "include",
      }
    );

    if (!response.ok) throw new Error('Failed to fetch evaluation info');
    const data = await response.json();
    console.log('Evaluation Info:', data);
    return data as Evaluation;
  } catch (error) {
    console.error('Error fetching evaluation info:', error);
    return null;
  }
}

export async function EDITEvaluationInfo(data: FormData ,classRoomCode: string,evaluationTitle: string): Promise<string> {
 
  try {
    const response = await fetch(`https://localhost:7273/api/profes/editEvaluation/${encodeURIComponent(classRoomCode)}/${encodeURIComponent(evaluationTitle)}`, {
      method: 'PUT',
      credentials: "include",
      body: data,
    });

    if (!response.ok) throw new Error('Failed to edit evaluation');
    const result = await response.json();
    console.log('Edited Evaluation:', result);
    return "success";
  } catch (error) {
    console.error('Error editing evaluation:', error);
    return "error";
  }
}

export async function GETGradeInfo(classCode: string, student_name: string, evaluation_name: string | null): Promise<Grade | null> {
  try {
    const response = await fetch(
      `https://localhost:7273/api/profes/grade/${encodeURIComponent(classCode)}/${encodeURIComponent(student_name)}/${encodeURIComponent(evaluation_name ?? "")}`,
      {
        method: 'GET',
        headers: { 'Content-Type': 'application/json' },
        credentials: "include",
      }
    );

    if (!response.ok) throw new Error('Failed to fetch grade info');
    const data = await response.json();
    console.log('Grade Info:', data);
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
  console.log(data); // You can adjust this as needed

  try {
    const response = await fetch(
      `https://localhost:7273/api/profes/editGrade/${encodeURIComponent(classRoomCode)}/${encodeURIComponent(student_name)}/${encodeURIComponent(evaluation_name ?? "")}`,
      {
        method: 'PUT',
        credentials: "include",
        body: data,
      }
    );

    if (!response.ok) throw new Error('Failed to edit grade');
    const result = await response.json();
    console.log('Edited Grade:', result);
    return "success";
  } catch (error) {
    console.error('Error editing grade:', error);
    return "error";
  }
}




export async function GETAllClassRooms(professor: string): Promise<ClassRoom[]> {
    let data;
    console.log(professor);
  try {
    const response = await fetch('https://localhost:7273/api/profes/ClassRooms',{
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
    });

    if (!response.ok) throw new Error('Failed to fetch');
    data = await response.json();
    console.log('Data:', data);
  } catch (err) {
    console.error('Error:', err);
  }
  // Assuming data is an array of ClassRoom objects
  const classrooms:ClassRoom[] = data['classRooms'] || [];
  console.log('Classrooms:', classrooms);
  return classrooms;
  
}

export async function EDITClassRooms(classroom: FormData):Promise<string>{
 // Assuming 'about' is a string, you might want to handle it differently if it's a file.
    console.log(classroom.get("classroomCode"))

  try {
    const response = await fetch(`https://localhost:7273/api/profes/edit/${classroom.get("classroomCode")}`, {
      method: 'PUT',
      credentials: "include",
      body:classroom,
    });
    if (!response.ok) throw new Error('Failed to edit classroom');
    const data = await response.json();
    console.log('Edited Classroom:', data);
  } catch (error) {
    console.error('Error editing classroom:', error);
    return "error";
  }   
  
  return "success";
}

export async function GETClassRoomsInfo(classroomCode: string): Promise<ClassRoom | null> {
  try{
    const response = await fetch(`https://localhost:7273/api/profes/edit/${classroomCode}`, {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
    });
    
    if (!response.ok) throw new Error('Failed to fetch classroom info');
    
    const data = await response.json();
    console.log('Classroom Info:', data);
    
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
    const response = await fetch(`https://localhost:7273/api/profes/delete/${classroomCode}`, {
      method: 'DELETE',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include"
    });

    if (!response.ok) {
      throw new Error('Failed to delete user');
      return "error";
    }

    const result = await response.json(); // or .text(), depending on API
    console.log('User deleted:', result);
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
    console.log('Profesor:', result);
    return true;
  } catch (error) {
    console.error('Error:', error);     
    return false;
  }

}
export async function SingUPaction(data: professor): Promise<boolean> {  
  console.log(data);
  console.log("JSON body:", JSON.stringify(data));
 
  try{
    const response = await fetch('https://localhost:7273/api/profes/SingUp', {
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
  console.log(name);
  try {
    const response = await fetch(`https://localhost:7273/api/profes/Profile`, {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
    });

    if (!response.ok) throw new Error('Failed to fetch profile info');
    const data = await response.json();
    console.log('Profile Info:', data);
    return data;
  } catch (error) {
    console.error('Error fetching profile info:', error);
    return null;
  }
}

export async function updateProfileInfo(data: Record<string, string | number>): Promise<void> {
  console.log(data);
  try {
    const response = await fetch('https://localhost:7273/api/profes/UpdateProfileInfo', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
      body: JSON.stringify(data),
    });

    if (!response.ok) throw new Error('Failed to update profile');
    const result = await response.json();
    console.log('Profile updated:', result);
  } catch (error) {
    console.error('Error updating profile:', error);
  }
}

export async function LogOutAction(): Promise<boolean> {
  try {
    const response = await fetch('https://localhost:7273/api/profes/LogOut', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: "include",
    });

    if (!response.ok) throw new Error('Failed to log out');
    console.log('Logged out successfully');
    return true;
  } catch (error) {
    console.error('Error logging out:', error);

  }

  return false;
}



export async function GETAboutFile(classroomCode: string ): Promise<Blob> {
        try{
        const response = await fetch(`https://localhost:7273/api/profes/aboutFile/${classroomCode}`, {
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
      `https://localhost:7273/api/profes/rubricFile/${encodeURIComponent(classRoomCode)}/${encodeURIComponent(evaluationTitle)}`,
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
      `https://localhost:7273/api/profes/statementFile/${encodeURIComponent(classRoomCode)}/${encodeURIComponent(evaluationTitle)}`,
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
        const response = await fetch(`https://localhost:7273/api/profes/feedBackFile/${encodeURIComponent(classroomCode)}/${encodeURIComponent(student_name)}/${encodeURIComponent(evaluation_name ?? "")}`, {
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


import React, { useState, useEffect } from 'react'
import styles from './CSS/ClassRoomsPage.module.css'
import {EDITClassRooms, GETAllClassRooms, SETClassRoom,DELETEClassRoom} from '../services/APIRequest.ts'
import { Link, useNavigate } from 'react-router-dom'
import {Card} from '../components/Card'
import {CSS} from '../utils/Functions.ts'
import { FloatFrame } from './FloatFrame.tsx'
import { ClassRoom} from '../interfaces/interfaces.ts'
import { TopSection } from '../components/TopSection.tsx'
import { ClassRoomForm } from '../components/ClassRoomForm.tsx'


export const ClassRoomsPage: React.FC = () => {
  const [classes, setClasses] = useState<ClassRoom[]>([]);
  const [float,setFloat] = useState(<></>)
  const navigate = useNavigate()

  useEffect(() => {
    const fetchClasses = async () => {
      const allClasses = await GETAllClassRooms("Profesor") as unknown as ClassRoom[];
      if(JSON.stringify(allClasses) !== JSON.stringify(classes)){
            setClasses(allClasses);
        }
    };
    fetchClasses();
  }, []);

  async function deleteClassroom(classroomCode:string):Promise<void>{
    const response:string = await DELETEClassRoom(classroomCode);
    const newClassrooms = classes.filter((classroom)=>classroom.classroomCode !== classroomCode)
    setClasses(newClassrooms)
  }

  async function handleFormSubmit(form: FormData, data:ClassRoom): Promise<void>{

    try{
      if(data.classroomCode===""){
        const  newclassroomCode: string = await SETClassRoom(form);
        data.classroomCode = newclassroomCode;
        setClasses((prevClasses) => [data,...prevClasses]);
      }else{
        const  response: string = await EDITClassRooms(form);
        const updatedClasses = classes.map((classroom) => {
          
          if (classroom.classroomCode === data.classroomCode) {
            return { ...classroom, ...data };
          }
          return classroom;
        });
        setClasses(updatedClasses);
     
      }
      clean_frame();
    }
    catch(error){
      console.error('Error saving classroom:', error);
    }
  }

  async function newClassroom(){
    renderClassroomForm(null)
  }

  async function renderClassroomForm(classRoomCode: string | null):Promise<void>{
    setFloat(<>
      <FloatFrame  closeFunction = {clean_frame}>
        <ClassRoomForm classroomCode={classRoomCode}
          handleSubmitClassRoomForm={handleFormSubmit}
          deleteClassRoom = {deleteClassroom}
          clean_frame={clean_frame}
        ></ClassRoomForm>
      </FloatFrame></>)
  }

  function clean_frame(){
    setFloat(<></>)
  }
  function openEvaluationPage(classroomCode: string, classRoomName: string){
    navigate(`${classroomCode}/${classRoomName}`)
  }
  return (
    <>
      <TopSection>
        <div>
          <h1>
            <Link to="/Home/ClassRooms">{'Classroom > Dashboard'}</Link>
          </h1>
        </div>
        <button onClick={newClassroom}>+ Classroom</button>

      </TopSection>
      <div className={CSS(styles, 'section')}>
        <div className={CSS(styles, 'class_Rooms')}>
          <ul>
            {classes.map((classroom) => (
              <li key={classroom.classroomCode}>
                <Card
                  data={classroom}
                  editCardFunc={renderClassroomForm}
                  clickCardFunc={() => {
                    openEvaluationPage(
                      classroom.classroomCode as string,
                      classroom.courseName as string
                    );
                  }}
                />
              </li>
            ))}
          </ul>
        </div>
      </div>
      {float}
    </>
  );
}

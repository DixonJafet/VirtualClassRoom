import React, { useEffect, useState } from 'react';
import { GeneralForm } from '../components/GeneralForm';
import { schema } from  '../interfaces/interfaces.ts';
import styles from './CSS/ProfilePage.module.css';
import { profileFormFormat } from '../utils/FormatForm';
import { FloatFrame } from './FloatFrame';
import { TopSection } from '../components/TopSection';
import { Link, useNavigate } from 'react-router-dom';
import {  updateProfileInfo,LogOutAction } from '../services/APIRequest.ts';


export const ProfilePage: React.FC = () => {
    const [profileInfo,setProfileInfo] = useState<schema[]>([]);
    const navigate = useNavigate();

    function singOut(): void {
      LogOutAction().then((result) => {     
        if (result) {
          navigate('/Login'); // Redirect to login page after logout
        }
      }).catch((error) => {
        console.error('Logout failed:', error);
      });

    }
    async function updateProfile(event: React.FormEvent<HTMLFormElement>): Promise<void> {
        event.preventDefault();
        const form = event.target as HTMLFormElement;
        const formData = new FormData(form);
        const data: Record<string, string | number > = {};
        formData.forEach((value, key: string) => { data[key] = value as string | number });
        const response = await updateProfileInfo(data);
        // Here you would typically send the data to your server
        // await updateProfileInfo(data);
    }


  useEffect(() => { async function getProfile(){
    const schema: schema[] = await profileFormFormat('Profesor');
    setProfileInfo(schema);}
    getProfile();}
    , []);

  return (<>
      <TopSection>
            <div>
              <h1><Link to='/Home/ClassRooms'>{'Classroom > '}</Link></h1>
              <h1><Link to={`/profile`}>{'Profile'}</Link></h1>
            </div>
            <button onClick={singOut}>{'█[→ Sing Out'}</button>
      </TopSection>
    <div className={styles.profileContainer}>
      <h1>Profile Settings</h1>
       <GeneralForm  formSchemas={profileInfo} handleSubmit={updateProfile}/>
    </div>  
    </>
  );
};
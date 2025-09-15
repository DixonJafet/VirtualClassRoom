import React, { useEffect } from "react";
import { schema } from  '../interfaces/interfaces.ts';
import { SingUPaction } from "../services/mockRequest.ts";
import {useNavigate} from 'react-router-dom'
import { useState } from "react";
import { profileFormFormat,loginFormFormat }   from '../utils/FormatForm';
import { GeneralForm } from "../components/GeneralForm";
import styles from './CSS/Login.module.css';
import { LogInAction } from "../services/mockRequest.ts";



export function Login(){
    const navigate = useNavigate(); 
    const [profileInfo,setProfileInfo] = useState<schema[]>([]);
    const [loginChecked, setLoginChecked] = useState(true);
    useEffect(() => {
        const fetchFormSchemas = async () => {
            const formSchemas = await loginFormFormat(null, null);
            setProfileInfo(formSchemas);
        };
        fetchFormSchemas();
    },[]);

async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const formData = new FormData(event.currentTarget);
        const data:{ [k: string]: FormDataEntryValue; }  = Object.fromEntries(formData.entries());
        if (loginChecked) {
            await loginAction(data);
        } else {
            await registerAction(data);
        }
    }

    async function registerAction(data: { [k: string]: FormDataEntryValue; }) {
        console.log(data);
        const { name, email,password} = data; // confirmPassword } = data;
        const phone: number = parseInt(data["phone"].valueOf() as string);
        const area_of_expertise = data["areaOfExpertise"]

        const response: boolean = await SingUPaction({
            name: name as string, 
            email: email as string,
            password: password as string,
            phone: phone,
            area_of_expertise: area_of_expertise as string  , 
           });
        if (response){ 
            console.log("Registro exitoso");
            navigate('/');
        }
    };

    async function loginAction(data:{ [k: string]: FormDataEntryValue; }) {
        const { email, password } = data;
        console.log(data);
        const response: boolean = await LogInAction({
            email: email as string,
            password: password as string,
        });
        if (response) {
            console.log("Login exitoso");
            navigate('/Home/ClassRooms');
        } else {
            console.error("Error en el login");
        }

  

      
    };

    async function handleChange(event: React.ChangeEvent<HTMLInputElement>){
        const { value } = event.target;
        let formSchemas: schema[] = [];
        if (value === "login") {
            setLoginChecked(true);
            formSchemas= await loginFormFormat(null,null);
            setProfileInfo(formSchemas);
        } else if (value === "singup") {
            setLoginChecked(false);
            formSchemas= await profileFormFormat(null);
            setProfileInfo(formSchemas);
        }
    }

    return(
<>
    <div className={styles.topSection}>
        <h1>Welcome to VirtualClass.Professor</h1>
    </div>
    <div className={styles.loginContainer}>
      <div className={styles.switchContainer}>
        <div>
                <input checked={loginChecked} type="radio" value="login" name="radio" id="login" onChange = {handleChange}/>
                <label htmlFor= "login"> Login</label>
        </div>
        <div>
                <input checked={!loginChecked} type="radio" value="singup" name="radio" id = "singup" onChange = {handleChange} />
                <label htmlFor = "singup"> Sing Up</label>
        </div>
      </div>
       <GeneralForm submitText={'Access'} formSchemas={profileInfo} handleSubmit={handleSubmit}/>
    </div>  
</>)
}
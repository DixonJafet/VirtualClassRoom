import { API_BASE_URL } from '../services/APIRequest';

export function CSS(styles:CSSModuleClasses,...list: string[]):string{

    const sentence = `${list.map(word => `${styles[word]}`).join(" ")}`;
    return sentence
  }


export async function checkCredentials(): Promise<boolean> {
      try{
          const response = await fetch(`${API_BASE_URL}/api/profes/checkCredentials`, {
            method: 'GET',    
            headers: {
              'Content-Type': 'application/json',
            },
            credentials: "include"
          });


          if (response.ok) {
            await response.json();
            return true;
          } else {
            return false;
          }


      }catch (error) {
          return false;
      }
}

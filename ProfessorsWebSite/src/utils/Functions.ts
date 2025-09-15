

export function CSS(styles:CSSModuleClasses,...list: string[]):string{

    const sentence = `${list.map(word => `${styles[word]}`).join(" ")}`;
    return sentence
  }


export async function checkCredentials(): Promise<boolean> {
      try{
          const response = await fetch('https://localhost:7273/api/profes/checkCredentials', {
            method: 'GET',    
            headers: {
              'Content-Type': 'application/json',
            },
            credentials: "include"
          });


          if (response.ok) {

            const data = await response.json();
            console.log('API check successful:', data);
            return true;

          } else {
            console.error('API check failed:', response.statusText);
            return false;
          }


      }catch (error) {
          console.error('Error checking credentials:', error);
          return false;
      }
}
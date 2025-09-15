/* eslint-disable @typescript-eslint/no-unused-vars */
/* eslint-disable prefer-const */
/* eslint-disable no-unused-vars */

import styles from './CSS/FloatFrame.module.css'
import { CSS } from '../utils/Functions'
import React, { ReactNode } from 'react'
interface Props{
    closeFunction: ()=>void;
    children?: ReactNode
}
export const FloatFrame: React.FC<Props> = ({
    closeFunction,
    children
}) =>{
    function close(event: React.MouseEvent<HTMLElement>){
        const button: HTMLButtonElement = event.target as  HTMLButtonElement ;
        let div: HTMLDivElement = button.closest(`.${styles['floating-frame']}`) as HTMLDivElement;
        closeFunction()
        div.style.display = 'none';
      //  closeFunction()
    }
    return(
        <>
            <div  className={CSS(styles,'floating-frame')}> 
                <div className={CSS(styles,'floating-frame-content')} >
                    <div className={CSS(styles,'floating-frame-content-header')}>
                    <button className={CSS(styles,'button-close')} onClick={close} aria-label="Close">
                        X
                    </button>
                    </div>
                    {children}
                </div>
            </div>
        </>
    )
}

import React from 'react';
import styles from './CSS/Dialog.module.css';
import { CSS } from '../utils/Functions';

interface Props {
    children: React.ReactNode;
    onAccept: () => void;
    onCancel: () => void;
    isOpen: boolean;
}

export const Dialog: React.FC<Props> = ({ children, onAccept, onCancel, isOpen }) => {
    const dialogRef = React.useRef<HTMLDialogElement>(null);

    function Accept(){
        onAccept();
        dialogRef.current?.close();
    }
    function Cancel(){
        onCancel();
        dialogRef.current?.close();
    }

    React.useEffect(() => {
        if (isOpen) {
            dialogRef.current?.showModal();
        } else {
            dialogRef.current?.close();
        }
    }, [isOpen]);

    return (
        <dialog ref={dialogRef} className={CSS(styles, 'dialog')}>
            {children}
            <div className={CSS(styles, 'button-container')}>
                <button onClick={Accept} className={CSS(styles, 'accept-button')}>
                    Accept
                </button>
                <button onClick={Cancel} className={CSS(styles, 'cancel-button')}>
                    Cancel
                </button>
            </div>
        </dialog>
    );
}; 
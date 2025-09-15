import styles from './CSS/Card.module.css'
import { CSS } from '../utils/Functions'

interface Schedule {
        day: string;
        time: string;
}

interface Props {
    data: {
        courseName?: string;
        classroomCode?: string;
        about?: string | File;                             
        schedule: Schedule[];
        [key: string]: unknown;
    };

    editCardFunc?: (cardNumber: string) => Promise<void>;
    clickCardFunc?: (cardNumber: string, classRoomName: string) => void;
}

export const Card: React.FC<Props> = ({ data, editCardFunc, clickCardFunc }) => {
    // Filter out the entries we don't want to show in front
    const filteredEntries = Object.entries(data).filter(([key]) => 
        !['courseName','aboutFile'].includes(key)
      
    );

    // Format schedule specifically
    const formatSchedule = (schedule: Schedule[]): JSX.Element => {
        return (
            <div className={CSS(styles, 'schedule-container')}>
                {schedule.map((day, index) => (
                    <div key={index} className={CSS(styles, 'schedule-day')}>
                        <span className={CSS(styles, 'day')}>{day.day}</span>
                        <span className={CSS(styles, 'time')}>{day.time}</span>
                    </div>
                ))}
            </div>
        );
    };

    // Format complex values (like arrays or objects)
    const formatValue = (key: string, value: unknown): JSX.Element | string => {
        if (key === 'schedule' && value && typeof value === 'object') {
            return formatSchedule(value as Schedule[]);
        }
        if (Array.isArray(value)) {
            return value.join(', ');
        }
        if (value && typeof value === 'object') {
            return Object.values(value).join(' ');
        }
        return String(value);
    };

    // Capitalize and format key names
    const formatKey = (key: string): string => {

        let formated:string =  key.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/^./, s => s.toUpperCase());
        let formated2: string  = formated.split('_')
            .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
            .join(' ');
         // insert space before capital
 
        return formated2;
    };

    return (
        <>
            <div className={CSS(styles,"card")}
                onClick={() => clickCardFunc?.( data.classroomCode as string, data.name as string)}
            >
                <div className={CSS(styles,"box","front")}>
                    <button className={CSS(styles,"edit")} onClick={(event) => {
                        event.stopPropagation()
                        editCardFunc?.( data.classroomCode as string)
                    }} >
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="16px" height="16px">
                            <path d="M 19.171875 2 C 18.448125 2 17.724375 2.275625 17.171875 2.828125 L 16 4 L 20 8 L 21.171875 6.828125 C 22.275875 5.724125 22.275875 3.933125 21.171875 2.828125 C 20.619375 2.275625 19.895625 2 19.171875 2 z M 14.5 5.5 L 3 17 L 3 21 L 7 21 L 18.5 9.5 L 14.5 5.5 z" fill="white"/>
                        </svg>
                    </button>
                    <div className={CSS(styles,'info')}> 
                        <div className={CSS(styles,'single-column')}>
                            {filteredEntries.map(([key, value]) => (
                                <div key={key} className={CSS(styles,'field', key.toLowerCase())}>
                                    <span className={CSS(styles,'key')}>{formatKey(key)}</span>
                                    <div className={CSS(styles,'value')}>
                                        {formatValue(key, value)}
                                    </div>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>
                <div className={CSS(styles,"box", "back")}>
                    <div className={CSS(styles,"row","title")}>
                        <span className={CSS(styles,"line")}></span>
                        <div className={CSS(styles,"column")} >
                            <h3><span>{data.courseName}</span></h3>
                        </div>
                    </div>
                </div>
            </div>
        </>
    )
}
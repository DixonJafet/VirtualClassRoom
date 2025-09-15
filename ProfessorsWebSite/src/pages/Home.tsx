import { Link, Outlet } from 'react-router'
import styles from './CSS/Home.module.css'
import { CSS } from '../utils/Functions'

export function Home(){
  return (
    <>{/*
      <nav className={CSS(styles,'nav-container')}>
        <ul>
          <li><Link to='ClassRooms'>ClassRooms</Link></li>
          <li><Link to='ClassRooms'>Profile</Link></li>
        </ul>
      </nav>*/}
      <Outlet/>
    </>
  )
}

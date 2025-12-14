import {  Outlet } from 'react-router-dom'
import styles from './CSS/Home.module.css'

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

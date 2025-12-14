/* eslint-disable @typescript-eslint/no-unused-vars */
/* eslint-disable prefer-const */

/* eslint-disable no-unused-vars */
import React from 'react'
import { EvaluationPage } from './pages/EvaluationPage.tsx'
import { ClassRoomsPage } from './pages/ClassRoomsPage.tsx'
import { Routes, Route, Navigate } from 'react-router-dom'
import { Home } from './pages/Home.tsx'
import { Login } from './pages/Login.tsx'
import { ProfilePage } from './pages/ProfilePage.tsx'

function App() {
  return (
    <>
      <Routes>
        <Route path="/" element={<Navigate replace to="/Login" />} />
        <Route path="/Login" element={<Login />} />
        <Route path="/Home" element={<Home />}>
          <Route path="ClassRooms" element={<ClassRoomsPage />} />
          <Route path="ClassRooms/:classroomCode/:ClassRoomName" element={<EvaluationPage />} />
        </Route>
        <Route path="/profile" element={<ProfilePage />} />
      </Routes>
    </>
  )
}

export default App
import { useEffect, useState,ReactNode } from "react";
import { Navigate } from "react-router-dom";
interface Props{
    Route?: string,
    children?: ReactNode
}
export const ProtectedRoute: React.FC<Props> = ({ children }) => {
  return  children;
};

//"http://localhost:5000/api/auth"


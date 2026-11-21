# 🧑‍🏫 VirtualClassRoom

**VirtualClassRoom** is a **virtual classroom management system** designed for professors — similar to **Google Classroom** — enabling the creation, organization, and management of courses and materials online.

---

## 🧩 Overview
This platform allows professors to manage their classes, assignments, and resources through a modern web interface with secure backend integration.

---

## 💻 Tech Stack

| Layer | Technology |
|-------|-------------|
| **Frontend** | ⚛️ ReactJS |
| **Backend** | 🧱 C# / .NET API |
| **Database** | 🐬 MySQL |
| **Deployment** | ☁️ Azure & ▲ Vercel |

---

## ☁️ Cloud Infrastructure (Azure & Vercel)

| Service | Purpose |
|----------|----------|
| **☁️ Azure App Service** | Hosts the backend API built with .NET |
| **🗄️ Azure Database for MySQL (Flexible Server)** | Stores the application’s relational data |
| **📦 Azure Storage Account** | Stores uploaded files and classroom resources |
| **▲ Vercel** | Deploys and serves the React frontend application |

---

## 🔒 Security
- Uses **JWT (JSON Web Tokens)** for secure authentication and browser session management  
- **Encrypted passwords** stored in the MySQL database  
- Ensures safe communication between frontend and backend using **HTTP-only cookies** and **HTTPS**
- Frontend API requests require HTTPS; set `VITE_API_URL` to an HTTPS URL when configuring a custom API host

---

## ⚙️ Features
- 👨‍🏫 Professor account management  
- 🧾 Class and assignment creation  
- 📁 File storage in Azure  
- 🔐 Secure authentication with JWT  
- 🌐 Deployed with Azure + Vercel for global scalability  

---

## 🤖 MCP Integration
The remote **Model Context Protocol (MCP)** server connects AI assistants to a professor's classrooms.

- 📊 Analyze grades, rankings, evaluation curves, and course progress
- 🧾 Manage classrooms, schedules, evaluations, grades, and attachments
- 🔐 Access is scoped to the authenticated professor; clients ask for confirmation before edits and deletions
- 🛠️ Built with **C# / ASP.NET Core**, the **MCP SDK**, **MySQL**, and **OAuth 2.0 / OpenIddict**

---

## 🚀 Quick Start
**Recomendation:**
I highly recommend use the mock User
email: **jafet.dexi@yahoo.com**
password: **password1234**

[Go to website.](https://virtual-class-room-peach.vercel.app/)

---

## 📄 License
This project is licensed under the [MIT License](LICENSE).

---

🧠 *Developed by Jafet Dixon*

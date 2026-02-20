# 🚀 Task Manager API & Frontend (JWT Auth)

A full-stack **Task Management System** built with **.NET 8 Web API** (Backend) and **Vanilla JavaScript** (Frontend). This project demonstrates secure authentication, modern REST API architecture, and interactive UI state management.

## 🌟 Key Features

- **🔐 Secure Authentication:** Full Login/Register system using **JWT (JSON Web Tokens)**.
- **📋 Task Management:** Create, Read, Update, and Delete (CRUD) personal tasks.
- **🖱️ Drag & Drop Interface:** Interactive UI to move tasks between *To Do*, *In Progress*, and *Done* statuses using HTML5 Drag & Drop API.
- **🛡️ Security First:** Protected API endpoints via `[Authorize]` attributes and a custom Middleware Pipeline.
- **🗄️ Database:** Microsoft SQL Server integration using **Entity Framework Core** (Code-First Approach).
- **🌐 CORS Support:** Configured for seamless communication between the frontend and backend.

## 🛠️ Technology Stack

| Component | Technology |
| :--- | :--- |
| **Backend** | .NET 8 (C#), ASP.NET Core Web API |
| **Database** | MS SQL Server, Entity Framework Core |
| **Frontend** | HTML5, CSS3 (Flexbox/Grid), Vanilla JavaScript |
| **Auth** | Microsoft.AspNetCore.Authentication.JwtBearer |
| **Docs** | Swagger / OpenAPI |

## 📂 Project Structure

```text
📦 SimpleAPIToDoList
 ┣ 📂 Controllers       # API Endpoints (Auth & Tasks)
 ┣ 📂 Data              # DB Context & EF Core Config
 ┣ 📂 Models            # User & TaskItem Entities
 ┣ 📂 Migrations        # Database Schema Versions
 ┣ 📂 frontend          # Client-side (HTML/CSS/JS)
 ┣ 📜 Program.cs        # Middleware Pipeline & Service Config
 ┗ 📜 README.md         # Project Documentation
```

## 🚀 Getting Started

### 1. Clone the Repository
```bash
git clone https://github.com/ilkmertdoner/SimpleAPIToDoList.git
```

### Configure appsettings.json

*Note: The configuration file is excluded for security purposes.*

Create a file named appsettings.json in the root of the Backend folder and add:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER_NAME;Database=TaskDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Key": "YOUR_SUPER_SECRET_KEY_MIN_32_CHARS",
    "Issuer": "TaskManagerApi",
    "Audience": "TaskManagerApi"
  }
}
```

### 3. Database Setup
Run this in the **Package Manager Console:**

```powershell
Update-Database
```

### 4. Run the Application
1. Start the API via Visual Studio or `dotnet run`.
2. Open `frontend/login.html` using a Live Server.

## 🔒 Security Implementation
* **Token Validation**: Backend strictly validates `JWT Issuer`, `Audience`, and `Lifetime`.
* **User Isolation**: Data is filtered by User ID: `Where(t => t.UserId == userId)`.

## 🖼️ Usage Guide
1. Register/Login to get your Bearer Token.
2. Add Tasks and manage them in the dashboard.
3. Drag & Drop tasks between status columns.

## 👤 Author
**İlkmert Döner**

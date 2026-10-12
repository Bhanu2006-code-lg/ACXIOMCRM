# AcxiomCRM

A full-featured, role-based CRM web application built with **ASP.NET Core MVC (.NET 10)**, **Entity Framework Core**, **ASP.NET Core Identity**, and **SQLite**.

---

## 🚀 Features

- ✅ Authentication & Authorization (Login, Register, Logout, Lockout)
- ✅ Role-Based Access Control — Admin, Manager, Sales Executive
- ✅ Dashboard with KPI Cards & Chart.js Charts
- ✅ Customer Management (CRUD + Search + Duplicate Check)
- ✅ Lead Management (CRUD + Status Workflow + Lead Conversion)
- ✅ Opportunity Management (Pipeline Stages + Weighted Amount)
- ✅ Follow-Up Management (Scheduled, Overdue Alerts)
- ✅ Activity Management (Call, Meeting, Email, Task)
- ✅ User & Role Management (Admin only)
- ✅ Audit Log (Login, Create, Update, Delete, Role Change)
- ✅ REST API (Customers, Leads, Opportunities, Pipeline Report)
- ✅ Reports (8 report types with filters)
- ✅ Client-side + Server-side + Business Validation
- ✅ Password Hashing, Policy & Account Lockout
- ✅ Anti-Forgery Protection on all forms

---

## 🛠️ Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core MVC (.NET 10) |
| ORM | Entity Framework Core |
| Database | SQLite (dev) / SQL Server (prod) |
| Auth | ASP.NET Core Identity |
| UI | Bootstrap 5, Bootstrap Icons |
| Charts | Chart.js |
| Validation | jQuery Unobtrusive Validation |

---

## 📁 Project Structure

```
AcxiomCRM/
├── Controllers/        # MVC + API Controllers
├── Models/             # Entity Models
├── ViewModels/         # View-specific models
├── DTOs/               # API Data Transfer Objects
├── Data/               # DbContext + Seeder
├── Services/           # AuditService
├── Views/              # Razor Views
└── wwwroot/            # CSS, JS, static files
```

---

## ⚙️ Getting Started

### Prerequisites
- .NET 10 SDK
- Visual Studio 2022 or VS Code

### Run Locally

```bash
git clone https://github.com/Bhanu2006-code-lg/ACXIOMCRM.git
cd ACXIOMCRM/AcxiomCRM
dotnet run
```

Open browser at `http://localhost:5050`

---

## 🔐 Demo Credentials

| Role | Email | Password |
|---|---|---|
| Admin | admin@acxiom.com | Admin@123 |
| Manager | manager@acxiom.com | Manager@123 |
| Sales Executive | sales@acxiom.com | Sales@123 |

---

## 📡 REST API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | /api/customers | List customers |
| GET | /api/customers/{id} | Get customer by ID |
| POST | /api/customers | Create customer |
| PUT | /api/customers/{id} | Update customer |
| DELETE | /api/customers/{id} | Deactivate customer |
| GET | /api/leads | List leads |
| POST | /api/leads | Create lead |
| GET | /api/opportunities | List opportunities |
| POST | /api/opportunities | Create opportunity |
| GET | /api/reports/pipeline | Pipeline report data |

---

## 🔒 Security

- Passwords hashed using ASP.NET Core Identity (PBKDF2)
- Account lockout after 5 failed attempts (15 min)
- Anti-forgery tokens on all POST forms
- Role-based authorization on every protected route
- Server-side validation on all inputs
- Audit logging for all security & business events

---

## 📊 Business Validation Rules

- Opportunity Amount must be > 0
- Probability must be between 0 and 100
- Expected Close Date cannot be in the past
- Follow-Up Date cannot be earlier than today
- Duplicate customer email/phone prevented
- Lead status transitions enforced

---

## 📄 License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

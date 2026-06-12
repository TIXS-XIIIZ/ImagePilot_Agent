# Blueprint: Local AI Image Batch Runner + AI Agent Extension

> โปรแกรม Local สำหรับสร้าง/จัดการ Prompt, ควบคุม Browser ที่ผู้ใช้ Login เอง, วาง Prompt อัตโนมัติ, รอผลลัพธ์, ดาวน์โหลดไฟล์, บันทึกประวัติลง SQL Server และเตรียมต่อยอดเป็นส่วนเสริมของ Local AI Agent ในอนาคต

---

## 1. เป้าหมายของโปรแกรม

โปรแกรมนี้ออกแบบมาเพื่อใช้งานส่วนตัวบนเครื่อง Local โดยมีเป้าหมายหลักคือ:

1. สร้าง Prompt สำหรับงานภาพด้วย AI/API ฟรีหรือ Local LLM
2. จัดคิวงานสร้างภาพจำนวนมาก
3. ควบคุม Browser ที่ผู้ใช้ Login เองไว้แล้ว
4. Copy/Paste Prompt ไปยังหน้าเว็บที่ผู้ใช้เปิดใช้งาน เช่น ChatGPT Web, Gemini Web, Google AI Studio หรือเว็บอื่น
5. รอให้การสร้างภาพเสร็จ
6. Save/Download ไฟล์ลงเครื่อง
7. Rename และจัดเก็บไฟล์ตาม Project
8. บันทึกประวัติ Prompt, Status, Output File, Error, Provider ลง SQL Server
9. เตรียมให้ AI Agent Local สามารถเรียกใช้งานเป็น Tool ได้ในอนาคต

---

## 2. ขอบเขตการใช้งาน

### ใช้สำหรับ

- ใช้งานส่วนตัวบนเครื่องตัวเอง
- ใช้ Browser Session ที่ผู้ใช้ Login เอง
- ใช้สำหรับ Workflow สร้างรูป / Prompt / Batch Generate
- ใช้ SQL Server Local หรือ SQL Server ใน LAN เพื่อเก็บประวัติ
- ต่อเป็น Local AI Agent Tool ได้ในอนาคต

### ไม่ควรใช้สำหรับ

- ทำเป็น SaaS ให้คนอื่นใช้บัญชีร่วมกัน
- Bypass Captcha หรือระบบยืนยันตัวตน
- Scrape หรือดึงข้อมูลที่ไม่ได้รับอนุญาต
- ยิงงานเร็วผิดธรรมชาติ
- เก็บ Password ของเว็บ AI ไว้ในโปรแกรม

---

## 3. Stack ที่แนะนำ

### Frontend / UI

ตัวเลือกที่ 1: React Local Dashboard

- React + Vite
- TailwindCSS
- ใช้ผ่าน localhost เช่น `http://localhost:5173`

ตัวเลือกที่ 2: Desktop App

- WPF / WinForms / Avalonia
- เหมาะถ้าต้องการทำเป็นโปรแกรม Windows เต็มตัว

### Local Backend

- ASP.NET Core Web API
- รันที่ `http://localhost:5000`
- ทำหน้าที่:
  - รับคำสั่งจาก UI
  - จัดการ Job Queue
  - เรียก Prompt Generator
  - สั่ง Browser Automation Worker
  - บันทึกข้อมูลลง SQL Server

### Browser Automation

- Playwright for .NET
- ใช้ Browser Profile แยกเฉพาะของโปรแกรม
- ไม่ใช้ Chrome Profile หลักของผู้ใช้โดยตรง

### Database

- Server name : DESKTOP-157NPTK\TIXSZOXIII
- password : tixs0013
- สร้าง table ที่จำเป็นได้
- SQL Server Local / SQL Server Express / SQL Server Developer Edition
- ใช้เก็บ:
  - Project
  - Prompt Job
  - Provider Setting
  - Run History
  - Error Log
  - Output File Metadata
  - Agent Tool Call Log

### File Storage

- เก็บรูปเป็นไฟล์จริงใน Folder
- Database เก็บเฉพาะ Path / Metadata

---

## 4. ภาพรวม Architecture

```text
[React / Desktop UI]
        |
        v
[ASP.NET Core Local API]
        |
        +--> [Prompt Generator Service]
        |
        +--> [Job Queue Service]
        |
        +--> [Browser Automation Worker: Playwright]
        |
        +--> [Download Manager]
        |
        +--> [File Manager]
        |
        +--> [SQL Server Repository]
        |
        v
[SQL Server + Local Output Folders]
```

---

## 5. Flow การทำงานหลัก

```text
1. User เปิดโปรแกรม
2. User เลือก Project
3. User ตั้งค่า Provider เช่น ChatGPT Web / Gemini Web / AI Studio
4. User ใส่ Base Prompt หรือ Template
5. ระบบสร้าง Prompt List
6. User ตั้งจำนวนงาน เช่น 10, 20, 50 jobs
7. User กด Start Batch
8. Backend สร้าง Job Queue
9. Playwright เปิด Browser Profile ที่ Login ไว้แล้ว
10. Worker วาง Prompt ลงหน้าเว็บ
11. Worker กดปุ่ม Generate
12. Worker รอผลลัพธ์
13. Worker กด Download หรือ Save
14. Download Manager ตรวจไฟล์ใหม่
15. File Manager Rename และ Move ไฟล์
16. SQL Server บันทึกสถานะ Completed
17. Worker ไป Job ถัดไป
18. เมื่อครบจำนวน ระบบสรุปผล
```

---

## 6. Module หลักของระบบ

### 6.1 Project Manager

หน้าที่:

- สร้าง Project ใหม่
- เปิด Project เดิม
- กำหนด Output Folder
- กำหนด Provider Default
- กำหนด Naming Pattern

ตัวอย่าง Project:

```text
Project Name: Mini Sandwich Poster
Output Folder: D:\AIOutput\MiniSandwich
Default Provider: ChatGPT Web
File Naming: mini_sandwich_{yyyyMMdd}_{jobNo}
```

---

### 6.2 Prompt Generator Service

หน้าที่:

- รับ Base Prompt
- ใช้ Template / Keyword / Style Preset เพื่อสร้าง Prompt หลายแบบ
- ใช้ API ฟรีหรือ Local LLM ช่วย Rewrite Prompt
- เก็บ Prompt ลง Job Queue

ตัวอย่าง Prompt Template:

```text
Create a realistic product poster for mini sandwiches.
Theme: cute, clean, appetizing, Thai menu style.
Main color: warm cream, accent purple.
Filling: {{filling}}
Layout: {{layout}}
Lighting: soft studio lighting.
Aspect ratio: {{ratio}}
```

ตัวแปร:

```json
{
  "filling": ["crab stick", "tuna", "ham cheese", "boiled egg", "fried egg"],
  "layout": ["flat lay", "front view", "menu poster", "product catalog"],
  "ratio": ["1:1", "9:16", "16:9"]
}
```

---

### 6.3 Job Queue Service

สถานะของ Job:

```text
Pending
Running
WaitingForResult
Downloading
Completed
Failed
Retrying
Skipped
Cancelled
```

กฎที่ควรมี:

- Retry ได้ตามจำนวนที่ตั้งไว้
- Pause / Resume ได้
- Stop ได้
- ถ้าเจอ Captcha หรือ Login หลุด ให้ Pause และรอ User แก้เอง
- ถ้า Timeout ให้ Mark Failed หรือ Retry

---

### 6.4 Browser Automation Worker

หน้าที่:

- เปิด Browser Profile แยกเฉพาะของโปรแกรม
- เข้า URL ของ Provider
- วาง Prompt
- กด Generate
- รอผลลัพธ์
- Download / Save
- ส่งสถานะกลับ Job Queue

หลักการสำคัญ:

```text
ไม่เก็บ Username/Password ของเว็บ AI
ไม่ Login แทน User
ให้ User Login เองใน Browser Profile
ไม่ Bypass Captcha
ถ้าเจอ Verification ให้ Pause แล้วให้ User จัดการเอง
```

---

### 6.5 Provider Config

รองรับหลายเว็บด้วย Config แยก:

```text
configs/
  chatgpt-web.json
  gemini-web.json
  google-ai-studio.json
  custom-provider.json
```

ตัวอย่าง Provider Config:

```json
{
  "providerName": "ChatGPT Web",
  "startUrl": "https://chatgpt.com/",
  "promptInputSelector": "textarea",
  "submitButtonSelector": "button[data-testid='send-button']",
  "resultContainerSelector": "[data-message-author-role='assistant']",
  "downloadButtonSelector": "button[aria-label*='Download']",
  "defaultTimeoutSeconds": 180,
  "delayBetweenJobsSeconds": 60,
  "manualModeFallback": true
}
```

หมายเหตุ: Selector ของหน้าเว็บเปลี่ยนได้บ่อย จึงควรให้แก้ไขผ่านหน้า Settings ได้

---

### 6.6 Download Manager

หน้าที่:

- ตรวจ Folder Downloads
- รอจนไฟล์ดาวน์โหลดเสร็จจริง
- ตรวจ `.crdownload` หรือไฟล์ชั่วคราว
- Rename ไฟล์
- Move ไป Output Folder
- บันทึก Path ลง Database

Naming Pattern ตัวอย่าง:

```text
{projectCode}_{provider}_{yyyyMMdd}_{jobNo}.{ext}
```

ผลลัพธ์:

```text
mini_sandwich_chatgpt_20260601_001.png
mini_sandwich_chatgpt_20260601_002.png
```

---

### 6.7 SQL Server Repository

หน้าที่:

- จัดการ Connection
- CRUD ข้อมูล Project / Job / Provider / Log
- ทำ Summary Report
- รองรับอนาคตให้ AI Agent Query History ได้

---

### 6.8 Local AI Agent Tool Interface

ในอนาคตสามารถเปิด API ให้ AI Agent เรียกได้ เช่น:

```text
POST /api/agent-tools/image-batch/create-project
POST /api/agent-tools/image-batch/create-jobs
POST /api/agent-tools/image-batch/start
POST /api/agent-tools/image-batch/pause
POST /api/agent-tools/image-batch/resume
GET  /api/agent-tools/image-batch/status/{runId}
GET  /api/agent-tools/image-batch/results/{runId}
```

ตัวอย่าง Agent Request:

```json
{
  "projectName": "Mini Sandwich Poster",
  "provider": "ChatGPT Web",
  "basePrompt": "Create cute mini sandwich menu poster",
  "count": 20,
  "outputFolder": "D:\\AIOutput\\MiniSandwich"
}
```

---

## 7. UI/UX Blueprint

### 7.1 Dashboard Page

แสดง:

- Project ปัจจุบัน
- Provider ปัจจุบัน
- จำนวน Jobs ทั้งหมด
- Completed
- Failed
- Running Job
- Preview Prompt ล่าสุด
- Preview Image ล่าสุด
- ปุ่ม Start / Pause / Resume / Stop

Layout:

```text
+--------------------------------------------------+
| Local AI Image Batch Runner                      |
+--------------------------------------------------+
| Project: Mini Sandwich Poster                    |
| Provider: ChatGPT Web                            |
| Status: Running                                  |
+------------------+-------------------------------+
| Total Jobs: 20   | Current Job: 05                |
| Completed: 04    | Failed: 00                     |
+------------------+-------------------------------+
| Current Prompt                                   |
| [Prompt Preview Area]                            |
+--------------------------------------------------+
| Latest Output                                    |
| [Image Preview Area]                             |
+--------------------------------------------------+
| [Start] [Pause] [Resume] [Stop]                  |
+--------------------------------------------------+
```

---

### 7.2 Project Page

Fields:

- Project Name
- Project Code
- Description
- Output Folder
- Default Provider
- File Naming Pattern
- Created Date
- Last Run Date

---

### 7.3 Prompt Builder Page

Fields:

- Base Prompt
- Style Preset
- Negative Prompt
- Variable List
- Number of Variations
- Language: Thai / English / Mixed
- Button: Generate Prompt List
- Button: Save Prompt Template

---

### 7.4 Provider Settings Page

Fields:

- Provider Name
- Start URL
- Prompt Input Selector
- Submit Button Selector
- Result Container Selector
- Download Button Selector
- Default Timeout
- Delay Between Jobs
- Manual Mode Fallback
- Browser Profile Path

---

### 7.5 SQL Server Settings Page

Fields ที่ให้ผู้ใช้กรอก:

```text
SQL Server Mode:
[ ] Windows Authentication
[ ] SQL Server Authentication

Server IP / Host:
[________________________________________]

Port:
[__________] default: 1433

Instance Name:
[________________________________________] optional

Database Name:
[________________________________________]

Username:
[________________________________________] optional if Windows Auth

Password:
[________________________________________] password input, masked

Encrypt:
[ ] True
[ ] False

Trust Server Certificate:
[ ] True
[ ] False

Test Connection Button:
[Test Connection]

Save Settings Button:
[Save]
```

คำแนะนำ:

- ถ้าใช้เครื่องตัวเอง แนะนำ Windows Authentication ก่อน
- ถ้าต้องใช้ SQL Login ให้เก็บ Password ใน Secret Store / Windows Credential Manager / Protected Local Storage
- ไม่ควรเก็บ Password เป็น Plain Text ใน `appsettings.json`
- ไม่ควรส่ง Password ไป React ย้อนกลับหลัง Save แล้ว

---

### 7.6 History / Gallery Page

แสดง:

- Project
- Provider
- Prompt
- Status
- Output File
- CreatedAt
- CompletedAt
- Error Message
- ปุ่ม Open Folder
- ปุ่ม Reuse Prompt
- ปุ่ม Re-run Job

---

## 8. SQL Server Connection Settings Template

### 8.1 แบบ Windows Authentication

```json
{
  "SqlServer": {
    "AuthMode": "Windows",
    "ServerHost": "127.0.0.1",
    "Port": "1433",
    "InstanceName": "",
    "Database": "LocalAiImageRunner",
    "Encrypt": true,
    "TrustServerCertificate": true
  }
}
```

Connection String ตัวอย่าง:

```text
Server=127.0.0.1,1433;Database=LocalAiImageRunner;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;
```

---

### 8.2 แบบ SQL Server Authentication

> ใช้เฉพาะกรณีจำเป็น และควรเก็บ Password ใน Secret Store ไม่ใช่ config ธรรมดา

```json
{
  "SqlServer": {
    "AuthMode": "SqlLogin",
    "ServerHost": "192.168.1.10",
    "Port": "1433",
    "InstanceName": "",
    "Database": "LocalAiImageRunner",
    "Username": "your_sql_user",
    "PasswordSecretKey": "LocalAiImageRunner:SqlPassword",
    "Encrypt": true,
    "TrustServerCertificate": true
  }
}
```

Connection String ตัวอย่างตอน Runtime:

```text
Server=192.168.1.10,1433;Database=LocalAiImageRunner;User Id=your_sql_user;Password={READ_FROM_SECRET_STORE};Encrypt=True;TrustServerCertificate=True;
```

---

## 9. พื้นที่สำหรับกรอก SQL Server Settings

> ส่วนนี้ใช้เป็น Template ในหน้า Settings หรือไฟล์ Setup Guide  
> ห้ามใส่รหัสผ่านจริงลง Git หรือส่งต่อให้ผู้อื่น

```text
[SQL SERVER CONFIG TEMPLATE]

Server IP / Host:
______________________________________________

Port:
______________________________________________

Instance Name:
______________________________________________

Database Name:
______________________________________________

Authentication Mode:
[ ] Windows Authentication
[ ] SQL Server Authentication

SQL Username:
______________________________________________

SQL Password:
______________________________________________
หมายเหตุ: ในโปรแกรมจริง ช่องนี้ควรเป็น Password Mask และ Save เข้าระบบ Secret/Encrypted Storage

Encrypt:
[ ] True
[ ] False

Trust Server Certificate:
[ ] True
[ ] False

Test Connection Result:
[ ] Success
[ ] Failed: __________________________________
```

---

## 10. Database Schema Draft

### 10.1 Create Database

```sql
CREATE DATABASE LocalAiImageRunner;
GO
```

---

### 10.2 Projects

```sql
CREATE TABLE dbo.Projects (
    ProjectId INT IDENTITY(1,1) PRIMARY KEY,
    ProjectName NVARCHAR(200) NOT NULL,
    ProjectCode NVARCHAR(100) NULL,
    Description NVARCHAR(MAX) NULL,
    OutputFolder NVARCHAR(1000) NOT NULL,
    DefaultProviderId INT NULL,
    FileNamingPattern NVARCHAR(300) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NULL,
    IsActive BIT NOT NULL DEFAULT 1
);
```

---

### 10.3 Providers

```sql
CREATE TABLE dbo.Providers (
    ProviderId INT IDENTITY(1,1) PRIMARY KEY,
    ProviderName NVARCHAR(200) NOT NULL,
    ProviderType NVARCHAR(100) NOT NULL,
    StartUrl NVARCHAR(1000) NOT NULL,
    PromptInputSelector NVARCHAR(500) NULL,
    SubmitButtonSelector NVARCHAR(500) NULL,
    ResultContainerSelector NVARCHAR(500) NULL,
    DownloadButtonSelector NVARCHAR(500) NULL,
    DefaultTimeoutSeconds INT NOT NULL DEFAULT 180,
    DelayBetweenJobsSeconds INT NOT NULL DEFAULT 60,
    ManualModeFallback BIT NOT NULL DEFAULT 1,
    BrowserProfilePath NVARCHAR(1000) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NULL,
    IsActive BIT NOT NULL DEFAULT 1
);
```

---

### 10.4 PromptTemplates

```sql
CREATE TABLE dbo.PromptTemplates (
    PromptTemplateId INT IDENTITY(1,1) PRIMARY KEY,
    ProjectId INT NULL,
    TemplateName NVARCHAR(200) NOT NULL,
    BasePrompt NVARCHAR(MAX) NOT NULL,
    NegativePrompt NVARCHAR(MAX) NULL,
    VariablesJson NVARCHAR(MAX) NULL,
    StylePreset NVARCHAR(200) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_PromptTemplates_Projects
        FOREIGN KEY (ProjectId) REFERENCES dbo.Projects(ProjectId)
);
```

---

### 10.5 BatchRuns

```sql
CREATE TABLE dbo.BatchRuns (
    BatchRunId INT IDENTITY(1,1) PRIMARY KEY,
    ProjectId INT NOT NULL,
    ProviderId INT NOT NULL,
    RunName NVARCHAR(200) NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    TotalJobs INT NOT NULL DEFAULT 0,
    CompletedJobs INT NOT NULL DEFAULT 0,
    FailedJobs INT NOT NULL DEFAULT 0,
    StartedAt DATETIME2 NULL,
    CompletedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ErrorMessage NVARCHAR(MAX) NULL,
    CONSTRAINT FK_BatchRuns_Projects
        FOREIGN KEY (ProjectId) REFERENCES dbo.Projects(ProjectId),
    CONSTRAINT FK_BatchRuns_Providers
        FOREIGN KEY (ProviderId) REFERENCES dbo.Providers(ProviderId)
);
```

---

### 10.6 PromptJobs

```sql
CREATE TABLE dbo.PromptJobs (
    PromptJobId INT IDENTITY(1,1) PRIMARY KEY,
    BatchRunId INT NOT NULL,
    ProjectId INT NOT NULL,
    ProviderId INT NOT NULL,
    JobNo INT NOT NULL,
    Prompt NVARCHAR(MAX) NOT NULL,
    NegativePrompt NVARCHAR(MAX) NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    RetryCount INT NOT NULL DEFAULT 0,
    MaxRetry INT NOT NULL DEFAULT 2,
    OutputFilePath NVARCHAR(1000) NULL,
    OutputFileName NVARCHAR(300) NULL,
    ErrorMessage NVARCHAR(MAX) NULL,
    StartedAt DATETIME2 NULL,
    CompletedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_PromptJobs_BatchRuns
        FOREIGN KEY (BatchRunId) REFERENCES dbo.BatchRuns(BatchRunId),
    CONSTRAINT FK_PromptJobs_Projects
        FOREIGN KEY (ProjectId) REFERENCES dbo.Projects(ProjectId),
    CONSTRAINT FK_PromptJobs_Providers
        FOREIGN KEY (ProviderId) REFERENCES dbo.Providers(ProviderId)
);
```

---

### 10.7 OutputFiles

```sql
CREATE TABLE dbo.OutputFiles (
    OutputFileId INT IDENTITY(1,1) PRIMARY KEY,
    PromptJobId INT NOT NULL,
    FilePath NVARCHAR(1000) NOT NULL,
    FileName NVARCHAR(300) NOT NULL,
    FileExtension NVARCHAR(50) NULL,
    FileSizeBytes BIGINT NULL,
    Width INT NULL,
    Height INT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_OutputFiles_PromptJobs
        FOREIGN KEY (PromptJobId) REFERENCES dbo.PromptJobs(PromptJobId)
);
```

---

### 10.8 AppSettings

```sql
CREATE TABLE dbo.AppSettings (
    SettingKey NVARCHAR(200) NOT NULL PRIMARY KEY,
    SettingValue NVARCHAR(MAX) NULL,
    IsSecret BIT NOT NULL DEFAULT 0,
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
```

หมายเหตุ: ถ้า `IsSecret = 1` ไม่ควรเก็บค่า Secret จริงในตารางนี้ ควรเก็บแค่ Reference Key เช่น `LocalAiImageRunner:SqlPassword`

---

### 10.9 AgentToolCalls

```sql
CREATE TABLE dbo.AgentToolCalls (
    AgentToolCallId INT IDENTITY(1,1) PRIMARY KEY,
    ToolName NVARCHAR(200) NOT NULL,
    RequestJson NVARCHAR(MAX) NULL,
    ResponseJson NVARCHAR(MAX) NULL,
    Status NVARCHAR(50) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CompletedAt DATETIME2 NULL,
    ErrorMessage NVARCHAR(MAX) NULL
);
```

---

## 11. API Endpoint Draft

### Project

```text
GET    /api/projects
POST   /api/projects
GET    /api/projects/{id}
PUT    /api/projects/{id}
DELETE /api/projects/{id}
```

### Provider

```text
GET    /api/providers
POST   /api/providers
PUT    /api/providers/{id}
POST   /api/providers/{id}/test-open
```

### Prompt

```text
POST   /api/prompts/generate
POST   /api/prompts/templates
GET    /api/prompts/templates
```

### Batch Run

```text
POST   /api/batch-runs
POST   /api/batch-runs/{id}/start
POST   /api/batch-runs/{id}/pause
POST   /api/batch-runs/{id}/resume
POST   /api/batch-runs/{id}/stop
GET    /api/batch-runs/{id}/status
GET    /api/batch-runs/{id}/jobs
```

### SQL Server Settings

```text
GET    /api/settings/sql-server
POST   /api/settings/sql-server/test
POST   /api/settings/sql-server/save
```

### Agent Tool

```text
POST   /api/agent-tools/image-batch/create
POST   /api/agent-tools/image-batch/start
GET    /api/agent-tools/image-batch/status/{batchRunId}
GET    /api/agent-tools/image-batch/results/{batchRunId}
```

---

## 12. Security Design

### 12.1 SQL Server Password

ห้าม:

```text
- ฝัง Password ใน React
- ส่ง Password กลับไปหน้า UI หลัง Save แล้ว
- Commit Password ลง Git
- เก็บ Password เป็น Plain Text ใน appsettings.json
```

ควร:

```text
- ใช้ Windows Authentication ถ้าเป็นเครื่อง Local
- ถ้าต้องใช้ SQL Login ให้เก็บใน Secret Store / Windows Credential Manager
- ใน DB เก็บแค่ Secret Key Reference
- หน้า UI แสดงเป็น ******** หลัง Save
```

---

### 12.2 Browser Login

ห้าม:

```text
- เก็บ Username/Password ของ ChatGPT/Gemini/AI Studio
- เขียนโค้ด Login แทน User
- Bypass Captcha
```

ควร:

```text
- ให้ User Login เอง
- ใช้ Browser Profile เฉพาะของโปรแกรม
- ถ้า Session หลุด ให้ Pause แล้วให้ User Login เอง
```

---

### 12.3 Local API Security

ถึงแม้เป็น Localhost ก็ควรมี:

```text
- Bind เฉพาะ localhost
- ไม่เปิด port ออก network ถ้าไม่จำเป็น
- มี Local Token ระหว่าง UI กับ Backend
- Log เฉพาะข้อมูลที่จำเป็น
```

---

## 13. Folder Structure

```text
LocalAiImageRunner/
│
├─ frontend/
│  └─ React UI
│
├─ backend/
│  └─ ASP.NET Core Local API
│
├─ worker/
│  └─ Browser Automation Worker
│
├─ configs/
│  ├─ appsettings.json
│  ├─ providers/
│  │  ├─ chatgpt-web.json
│  │  ├─ gemini-web.json
│  │  └─ ai-studio.json
│
├─ browser-profiles/
│  ├─ chatgpt/
│  ├─ gemini/
│  └─ ai-studio/
│
├─ projects/
│  └─ mini-sandwich/
│     ├─ prompts/
│     ├─ outputs/
│     └─ logs/
│
└─ docs/
   └─ blueprint.md
```

---

## 14. Roadmap

### MVP v1

เป้าหมาย: ใช้งาน Batch Prompt ได้จริงแบบง่าย

- Project CRUD
- Provider Config
- Prompt List แบบกรอกเอง
- Start/Pause/Stop
- Playwright เปิดเว็บ
- Paste Prompt
- Manual Download หรือ Auto Download แบบพื้นฐาน
- Save Log ลง SQL Server

---

### MVP v2

เป้าหมาย: เพิ่มความอัตโนมัติ

- Prompt Generator
- Template Variables
- Auto Download Manager
- Gallery Page
- Retry / Timeout
- Provider Selector
- SQL Server Settings UI
- Test Connection

---

### MVP v3

เป้าหมาย: ต่อกับ AI Agent Local

- Agent Tool API
- Workflow Runner
- Agent สามารถสร้าง Project + Jobs ได้
- Agent อ่าน Status / Results ได้
- เพิ่ม Local RAG / Wiki Integration
- เพิ่ม Export Report

---

### MVP v4

เป้าหมาย: Creative Automation Suite

- Image Batch
- Video Prompt Batch
- Music Prompt Batch
- E-book Cover Workflow
- Product Poster Workflow
- Prompt Library Marketplace แบบ Local
- Dashboard วิเคราะห์จำนวนงาน / เวลา / Provider

---

## 15. ข้อควรระวังในการทำจริง

1. หน้าเว็บเปลี่ยน UI แล้ว Selector อาจพัง
2. เว็บอาจมี Captcha หรือ Verification
3. ควรมี Manual Fallback เสมอ
4. อย่าทำ Batch เร็วเกินธรรมชาติ
5. อย่าเปิด User Data Directory เดียวกับ Chrome หลัก
6. อย่าเก็บ Password ของเว็บ AI
7. เก็บ SQL Password แบบ Secret หรือ Encrypt เท่านั้น
8. ควรทำ Log ให้ละเอียดเพื่อ Debug ง่าย

---

## 16. Development Task Breakdown

### Phase 1: Backend Foundation

- สร้าง ASP.NET Core Web API
- ตั้งค่า SQL Server Connection
- สร้าง Database Schema
- ทำ Repository
- ทำ Project API
- ทำ Provider API
- ทำ BatchRun API

### Phase 2: Frontend Foundation

- สร้าง React + Vite
- Dashboard Page
- Project Page
- Provider Settings Page
- SQL Server Settings Page
- Batch Job Page

### Phase 3: Worker

- ติดตั้ง Playwright
- เปิด Browser Profile
- Test เปิด URL
- Test Paste Prompt
- Test Click Generate
- Test Wait Result
- Test Download

### Phase 4: Job Queue

- Pending / Running / Completed / Failed
- Pause / Resume / Stop
- Retry
- Timeout
- Error Log

### Phase 5: Prompt Generator

- Prompt Template
- Variable Randomizer
- Free API / Local LLM Hook
- Save Prompt List

### Phase 6: Agent Extension

- Agent Tool Endpoints
- Request/Response JSON Schema
- AgentToolCalls Log
- Status Polling
- Result Export

---

## 17. Minimal Working Version

ถ้าจะเริ่มทำให้เร็วที่สุด ให้ทำแค่นี้ก่อน:

```text
1. SQL Server Settings
2. Provider Config: URL + Prompt Selector + Submit Selector
3. Prompt List
4. Start Batch
5. Playwright Paste Prompt
6. User Manual Download
7. Save Job Status ลง SQL Server
```

หลังจากนั้นค่อยเพิ่ม Auto Download และ Prompt Generator

---

## 18. สรุป

โปรแกรมนี้ควรเริ่มจาก Local Batch Tool ก่อน แล้วค่อยขยายเป็น Local AI Agent Extension

Core ที่ต้องมีตั้งแต่แรก:

```text
- SQL Server เป็นฐานข้อมูลหลัก
- Output File อยู่ใน Folder
- Browser Profile แยก
- Provider Config แก้ไขได้
- Job Queue มี Pause/Resume/Stop
- Secret แยกจาก Config
- API เผื่อ Agent เรียกใช้ภายหลัง
```

แนวคิดสำคัญ:

```text
UI ใช้จัดการ
Backend ใช้ควบคุม
Worker ใช้ทำงาน
SQL Server ใช้จำ
Folder ใช้เก็บไฟล์จริง
AI Agent ใช้สั่งงานในอนาคต
```

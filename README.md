# Group Chat Application

A simple, robust, and modern group chat application built with C# using TCP Socket programming.

The project includes:

- A Console-based Asynchronous Chat Server
- A WPF Chat Client with a modern, fully customized dark-themed UI
- Clean MVVM architecture for the client-side

---

## ✨ Features

### 💬 Real-time Messaging
Send and receive messages instantly from other connected clients through TCP socket communication.

### 👥 Live Online Count
Accurately tracks and displays the real-time number of active users currently connected to the chat room.

### 😀 Advanced Emoji Support
Includes a custom-built interactive emoji picker powered by `Emoji.Wpf`.

Features:
- Dynamically loads emoji data from a local `emoji.json` database
- Horizontally scrollable category tabs
- Hover animations and tooltips
- Full-color emoji rendering

### 🔄 Auto-Scrolling
The chat list automatically scrolls to the newest message whenever a new message is received or sent.

### 🛡 Connection Management & Safety
- Prevents spam-clicking during connection attempts
- Handles connection timeouts safely
- Displays loading states while connecting
- Supports IP and Port validation

### ✅ Form Validation & Sanitization
- Username validation
- IPv4 format validation
- Port range validation
- Automatic message trimming before sending

### ⌨ Smart Input
- `Enter` → Send message
- `Shift + Enter` → Create a new line

### 📢 System Notifications
Automatically broadcasts system messages when users join or leave the chat room.

### 🧩 Clean MVVM Architecture
Strict separation between UI and business logic using:
- MVVM Pattern
- Attached Behaviors
- Commands
- Data Binding

---

## 🛠 Technologies Used

- C# / .NET 8.0+
- WPF (Windows Presentation Foundation)
- TCP Socket (`TcpListener` & `TcpClient`)
- MVVM Pattern
- JSON Serialization (`System.Text.Json`)

### 📦 NuGet Packages

- `Emoji.Wpf`

---

## 📂 Project Structure

```text
GroupChat/
├── GroupChat_Client/
│   ├── Behaviors/
│   │   ├── TextBoxEnterBehavior.cs
│   │   └── ListBoxScrollBehavior.cs
│   │
│   ├── Commands/
│   │   ├── RelayCommand.cs
│   │   └── RelayCommand<T>.cs
│   │
│   ├── Models/
│   │   ├── ChatMessage.cs
│   │   ├── EmojiModel.cs
│   │   └── EmojiProvider.cs
│   │
│   ├── ViewModels/
│   │   ├── MainViewModel.cs
│   │   └── ChatViewModel.cs
│   │
│   ├── Views/
│   │   ├── MainWindow.xaml
│   │   ├── MainWindow.xaml.cs
│   │   ├── ChatWindow.xaml
│   │   └── ChatWindow.xaml.cs
│   │
│   ├── db/
│   │   └── emoji.json
│   │
│   ├── App.xaml
│   └── App.xaml.cs
│
├── GroupChat_Server/
│   └── Program.cs
│
├── .gitignore
└── README.md
```

---

## 🚀 How to Run

### 1️⃣ Restore Packages

Before running the client project:

- Restore all NuGet packages
- Make sure `Emoji.Wpf` is installed
- Set `emoji.json` property to:
  - **Build Action:** Content
  - **Copy to Output Directory:** Copy if newer

---

### 2️⃣ Start the Server

Run the `GroupChat_Server` project first.

The server will display the assigned IP address and port.

Example:

```text
Server started
==============================
Port: 5000

Connect using one of these IPs:
IP: 192.168.1.X    Port: 5000

If client is on the same computer:
IP: 127.0.0.1    Port: 5000
==============================
```

---

### 3️⃣ Start the Client

Run the `GroupChat_Client` WPF project.

Enter the server information:

```text
Username: your_name
Server IP: 127.0.0.1
Port: 5000
```

Then click **Connect**.

---

### 4️⃣ Test Group Chat

To test the application:

- Open multiple client instances
- Connect them to the same server
- Send messages between clients

Features you can test:
- Real-time messaging
- Emoji picker
- Online user counter
- Auto-scrolling
- Join/Leave system notifications

---

## 🏗 MVVM Architecture

This project strictly follows the MVVM (Model-View-ViewModel) pattern to ensure scalability and maintainability.

### 📌 Model
Stores application data and structures.

Examples:
- `ChatMessage`
- `EmojiModel`
- `EmojiProvider`

---

### 🎨 View
Contains all UI layouts written in XAML.

Examples:
- `MainWindow.xaml`
- `ChatWindow.xaml`

The Views contain minimal or no business logic.

---

### 🧠 ViewModel
Handles:
- Socket communication
- Data binding
- Commands
- Application logic

Examples:
- `MainViewModel`
- `ChatViewModel`

---

### ⚡ Behaviors & Commands
Used to decouple UI interactions from the ViewModels.

Examples:
- Sending messages with Enter key
- Automatic list scrolling
- Button commands

---

## 💡 Notes

- The server must be running before any client connects
- If the server uses a random port (`Port = 0`), clients must use the exact generated port
- Use `127.0.0.1` if testing on the same computer
- Use the local IPv4 address (`192.168.x.x`) for LAN testing

---

## 📸 Recommended Future Improvements

Possible features to add in the future:

- Private messaging
- File sharing
- Message history persistence
- Authentication system
- Encrypted communication
- Voice chat support
- Server room system
- User avatars

---

## ✍️ Author

**Group Chat Client / Server Project**

Built with C#, WPF, TCP Socket, and MVVM architecture.

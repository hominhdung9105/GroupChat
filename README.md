# Group Chat Application

A simple group chat application built with C# using TCP Socket programming.

The project includes:

- A Console-based Chat Server
- A WPF Chat Client
- MVVM architecture for the client side

## Features

- Connect to server using IP and Port
- Enter username before joining chat
- Open separate chat window after connecting
- Send messages to other connected clients
- Receive messages from other users in real time
- Show username and server IP in chat window
- Display system messages when users connect or disconnect

## Technologies Used

- C#
- .NET
- WPF
- TCP Socket
- MVVM Pattern

## Project Structure

```text
GroupChat/
├── GroupChat_Client/
│   ├── Commands/
│   │   └── RelayCommand.cs
│   ├── Models/
│   │   └── ChatMessage.cs
│   ├── ViewModels/
│   │   ├── MainViewModel.cs
│   │   └── ChatViewModel.cs
│   ├── Views/
│   │   ├── MainWindow.xaml
│   │   ├── MainWindow.xaml.cs
│   │   ├── ChatWindow.xaml
│   │   └── ChatWindow.xaml.cs
│   ├── App.xaml
│   └── App.xaml.cs
│
├── GroupChat_Server/
│   └── Program.cs
│
├── .gitignore
└── README.md
```

## Client Screens

### Main Window

The main window is used to enter connection information:

- Username
- Server IP
- Port

After clicking the Connect button, the chat window will open.

### Chat Window

The chat window is used to:

- View connected username
- View server IP
- Send messages
- Receive messages from other users

## How to Run

### 1. Start the Server

Run the server project first.

The server will display the IP address and port in the console.

Example:

```text
Server started
IP: 127.0.0.1
Port: 5000
```

### 2. Start the Client

Run the WPF client project.

Enter the server information:

```text
Username: user1
Server IP: 127.0.0.1
Port: 5000
```

Then click Connect.

### 3. Test Group Chat

To test group chat, open more than one client instance.

Example:

```text
Client 1: user1
Client 2: user2
```

When user1 sends a message, user2 will receive it.

When user2 sends a message, user1 will receive it.

## MVVM Explanation

This project uses the MVVM pattern.

### Model

Models store application data.

Example:

- ChatMessage

### View

Views are the UI files.

Example:

- MainWindow.xaml
- ChatWindow.xaml

### ViewModel

ViewModels handle UI logic and data binding.

Example:

- MainViewModel
- ChatViewModel

## Notes

- The server must be running before the client connects
- If the server uses a random port, the client must enter the new port shown in the server console
- If testing on the same computer, use `127.0.0.1` as the server IP
- If testing on another computer in the same network, use the local network IP shown by the server

## Author

Group Chat Client / Server project
"# GroupChat" 
"# GroupChat" 

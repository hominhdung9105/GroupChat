# Group Chat Application

A simple and modern group chat application built with C# using TCP Socket programming.

The project includes:

- A Console-based Chat Server
- A WPF Chat Client with a modern, dark-themed UI
- Clean MVVM architecture for the client side

## Features

- **Connection Management:** Connect to the server using a valid IP and Port
- **Form Validation:** Input validation for Username, IP formatting, and Port ranges
- **Real-time Messaging:** Send and receive messages instantly from other connected clients
- **Emoji Support:** Built-in interactive emoji picker rendered in full color (powered by `Emoji.Wpf`)
- **Smart Input:** Press `Enter` to send a message, or `Shift + Enter` to create a new line
- **System Notifications:** Display system messages when users connect or disconnect from the room
- **Clean Architecture:** Strict separation of UI and logic using MVVM pattern and Attached Behaviors

## Technologies Used

- C# / .NET
- WPF (Windows Presentation Foundation)
- TCP Socket (`TcpListener` & `TcpClient`)
- MVVM Pattern
- NuGet Packages:
  - `Emoji.Wpf`

## Project Structure

```text
GroupChat/
├── GroupChat_Client/
│   ├── Behaviors/
│   │   └── TextBoxEnterBehavior.cs
│   ├── Commands/
│   │   ├── RelayCommand.cs
│   │   └── RelayCommand<T>.cs
│   ├── Models/
│   │   ├── ChatMessage.cs
│   │   └── EmojiProvider.cs
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

## How to Run

### 1. Restore Packages

Before running the client, restore the NuGet packages for the project, especially the `Emoji.Wpf` package used in `GroupChat_Client`.

### 2. Start the Server

Run the server project first.

The server will display the IP address and port in the console.

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

### 3. Start the Client

Run the WPF client project.

Enter the server information:

```text
Username: user1
Server IP: 127.0.0.1
Port: 5000
```

Then click **Connect**.

### 4. Test Group Chat

To test group chat, open more than one client instance.

When `user1` sends a message, `user2` will receive it instantly.

## MVVM Explanation

This project strictly adheres to the MVVM pattern.

### Model

Stores application data and states.

Examples:

- `ChatMessage`
- `EmojiProvider`

### View

The UI files written in XAML.

Examples:

- `MainWindow.xaml`
- `ChatWindow.xaml`

Contains no business logic in the code-behind.

### ViewModel

Handles UI logic, data binding, and commands.

Examples:

- `MainViewModel`
- `ChatViewModel`

### Behaviors & Commands

Used to decouple UI events such as keyboard inputs from the ViewModels.

## Notes

- The server must be running before the client connects
- If the server uses a random port (`Port = 0`), the client must enter the exact new port shown in the server console
- If testing on the same computer, use `127.0.0.1` as the server IP
- If testing on another computer in the same network, use the local network IP shown by the server (for example: `192.168.x.x`)

## Author

Group Chat Client / Server project

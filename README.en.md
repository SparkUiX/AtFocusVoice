# AtFocusVoice

## Language Versions

- [中文版本](README.md)

## Introduction

AtFocusVoice is a WPF application designed to automatically control the audio of selected processes. When a selected process loses focus, the application mutes its audio; when the process regains focus, the volume is automatically restored. This software is ideal for users who want to switch between different applications without interfering with their volume settings.

## Features

- Select and manage processes
- Automatic mute and volume restoration
- User-friendly interface

## Usage

1. Start the application
2. Select the processes you want to manage
3. Click the "Enable" button to start automatic audio control
4. Enjoy a seamless audio switching experience

## Installation

If you are using the release version:
- If your computer does not have the .NET runtime installed, please choose `AtFocusVoice.exe` for download.
- If your computer already has the .NET runtime installed, please choose `AtFocusVoiceWithRuntime.exe` for download.

## Open Source License

This project is licensed under the MIT License. Contributions and feedback are welcome.

## Contributing

If you would like to contribute to this project, please submit a Pull Request or open an Issue.

## Known Issues

- May not correctly identify processes in certain cases
- 
- Audio switching may experience delays when switching windows
- 
- May not correctly recognize audio devices in certain situations
- 
- The application is a windowed process and does not run in the background, so you need to keep the window open to ensure the program functions correctly.
-
- The process list does not update in real-time; you need to press F5 or reopen the application to refresh the list.
-
- The enabled status of the processes is saved in your AppData\Roming\AtFocusVoice folder, and you can manually modify or delete it.


If you have any questions or suggestions, please feel free to contact me.

You can reach me at thoe9008@outlook.com, as I may not check GitHub messages promptly.

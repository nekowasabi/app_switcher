# AppSwitcher

A lightweight Windows utility for quickly switching between applications.

You can edit this file directly with any text editor. After editing, restart the application to apply the changes.

## System Requirements

- Windows 10 or later
- .NET 6.0 Runtime

AppSwitcher is a Windows utility that allows you to quickly switch back to your previous application using a customizable hotkey.

## Features

- Runs in the system tray
- Switches to the previous application with a customizable hotkey
- Configurable through an INI file
- Minimal resource usage

## Usage

1. Run the application. It will appear in your system tray.
2. Press the configured hotkey (default: Alt+Q) to switch to the previous application.
3. Right-click the tray icon to access settings or exit the application.

## Configuration

The application uses a settings.ini file located in the same directory as the application executable. You can customize the following settings:

```ini
; AppSwitcher Settings
; Modifiers: ALT, CTRL, SHIFT, WIN (comma separated)
Modifiers=ALT
; Key: Any key from the Keys enum (e.g., Q, W, F1, etc.)
Key=Q
```
## Development

### Building the Project

The project is built using .NET 6.0 with Windows Forms:

```bash
dotnet build
```

### Null Reference Handling

This project uses C# nullable reference types to prevent null reference exceptions:
- Event handlers are marked as nullable with `?` where appropriate
- Null checks are implemented for dictionary lookups and path operations
- System.Windows.Forms.Timer is fully qualified to avoid ambiguous references

## License

MIT

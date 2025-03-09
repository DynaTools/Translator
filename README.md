# Clipboard Translator

![Clipboard Translator Logo](Resources/translate_icon.png)

A desktop application that automatically translates clipboard content in real-time, designed to increase productivity in multilingual communications.

## Features

- **Automatic Clipboard Monitoring**: Detects when you copy text and automatically translates it
- **Multiple Languages**: Supports Portuguese, English, Italian, Spanish, French, German
- **Translation Tones**: Choose between Neutral, Formal, Casual, Technical, and Professional tones
- **AI Translation Services**: 
  - Google Gemini Flash API integration
  - OpenAI API integration
- **Customization Options**:
  - Adjust AI parameters (temperature, top-p, frequency penalty, etc.)
  - Compare different translation tones
  - Set token limits for translations
- **System Integration**:
  - System tray icon for easy access
  - Start with Windows option
  - Minimize to tray when closing
- **User-Friendly Interface**:
  - Translation preview with editing capability
  - Language swap button
  - Daily translation statistics

## Screenshots

*[Add screenshots here]*

## Installation

### Requirements
- Windows OS
- .NET 6.0 Runtime

### Installation Steps
1. Download the latest release from the [Releases](https://github.com/yourusername/ClipboardTranslator/releases) page
2. Extract the zip file to a location of your choice
3. Run `Translator.exe` to start the application

## Setting Up API Keys

Clipboard Translator requires an API key from either Google Gemini or OpenAI to function:

### Google Gemini Flash API
1. Go to [Google AI Studio](https://ai.google.dev/tutorials/ai-studio_quickstart)
2. Sign up and obtain an API key
3. In Clipboard Translator, go to Settings → API Keys
4. Enter your Gemini API key and select "Google Gemini Flash API" as preferred service

### OpenAI API
1. Go to [OpenAI API Keys](https://platform.openai.com/api-keys)
2. Sign up and create a new API key
3. In Clipboard Translator, go to Settings → API Keys
4. Enter your OpenAI API key and select "OpenAI API" as preferred service

## How to Use

### Basic Usage
1. Start the application (it will appear in your system tray)
2. Select source and target languages from the dropdown menus
3. Copy any text to your clipboard (Ctrl+C)
4. The text will be automatically translated and placed back in your clipboard
5. Paste the translated text where needed (Ctrl+V)

### Configure Translation Settings
- **Languages**: Select source and target languages from the dropdown menus
- **Tone**: Choose the appropriate tone for your translations
- **AI Parameters**: Adjust parameters like temperature and top-p to fine-tune translations

### Advanced Features
- **Edit Translations**: Modify the translated text and apply your edits back to the clipboard
- **Compare Tones**: View the same text translated with different tones
- **Token Limit**: Set maximum token length for translations to prevent excessive API usage

## Preferences

Access preferences from Settings → Preferences:

- **Start with Windows**: Launch application automatically at system startup
- **Start minimized**: Start the application minimized to the system tray
- **Play sound**: Play a notification sound when translation completes
- **Minimize to tray when closing**: Minimize instead of closing when the X button is clicked
- **Token limit**: Set maximum text length for translation

## Building from Source

1. Clone the repository
   ```
   git clone https://github.com/yourusername/ClipboardTranslator.git
   ```
2. Open the solution in Visual Studio 2022
3. Restore NuGet packages
4. Build the solution

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Developed with Claude's assistance
- Icon: [Translation icon created by Freepik - Flaticon](https://www.flaticon.com/free-icons/translation)

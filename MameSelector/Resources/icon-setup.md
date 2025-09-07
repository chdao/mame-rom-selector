# Icon Setup Instructions

## Required Icon Files

You'll need to create these icon files and place them in the `MameSelector/Resources/` folder:

1. **app.ico** - Main application icon (16x16, 32x32, 48x48, 64x64, 128x128, 256x256)
2. **app-16.png** - 16x16 PNG version
3. **app-32.png** - 32x32 PNG version
4. **app-48.png** - 48x48 PNG version
5. **app-64.png** - 64x64 PNG version
6. **app-128.png** - 128x128 PNG version
7. **app-256.png** - 256x256 PNG version

## Icon Creation Tools

### Online Tools:
- **Favicon.io** - Generate ICO files from PNG
- **ConvertICO** - Convert PNG to ICO
- **RealFaviconGenerator** - Multi-format icon generator

### Desktop Tools:
- **GIMP** - Free image editor with ICO export
- **Paint.NET** - Windows image editor
- **Visual Studio** - Built-in icon editor

## Icon Design Tips

### For MAME ROM Selector:
- Use **gaming/arcade** theme
- Consider **controller + file** combination
- Use **retro/8-bit** color palette
- Keep it **simple and recognizable** at small sizes
- Use **high contrast** for visibility

### Technical Requirements:
- **ICO format** for Windows application
- **Multiple sizes** (16x16 to 256x256)
- **PNG format** for modern displays
- **Transparent background** preferred
- **Square aspect ratio**

## Implementation

Once you have the icon files, the application will automatically use them. The icon will appear in:
- Application window title bar
- Taskbar
- Alt+Tab switcher
- File explorer
- Start menu (if installed)

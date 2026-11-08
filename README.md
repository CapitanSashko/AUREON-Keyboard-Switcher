# AUREON Keyboard Switcher

**English** | [Українська]([README.uk.md](https://github.com/CapitanSashko/AUREON-Keyboard-Switcher/blob/main/AUREON%20Keyboard%20Switcher/README.uk.md))

<img width="786" height="617" alt="Знімок екрана 2026-11-08 145313" src="https://github.com/user-attachments/assets/d444a8bd-7977-4ccb-b1d0-a66f37588a19" />

**AUREON Keyboard Switcher** is a lightweight Windows background application designed to automatically correct text typed in the wrong keyboard layout. It also provides intelligent case correction, Cyrillic-to-Latin transliteration, and a self-learning user dictionary.

The application runs in the background and helps improve typing speed and comfort by eliminating the need to manually delete and retype incorrectly entered text.

## Table of Contents

- [Features](#features)
- [Hotkeys](#hotkeys)
- [Automatic Correction](#automatic-correction)
- [Operational Details](#operational-details)
- [Settings and Logging](#settings-and-logging)
- [System Integration](#system-integration)

## Features

### Automatic Layout Correction

Detects text typed using the wrong keyboard layout and automatically converts it to the intended language.

Corrections can be performed automatically or manually using the `Pause` key. Automatic corrections can also be immediately undone.

### Self-Learning Dictionary

When an automatic correction is undone using `Pause`, the application can add the original word to the custom user dictionary.

Once added, the word will no longer be automatically corrected.

- Self-learning can be enabled or disabled in the settings.
- User-defined words can be managed in the **Dictionary** section.
- Dictionary data is stored in `settings.json`, under the `UserWords` field.
- The dictionary can be transferred between computers.

### Case Correction

Automatically detects and corrects common capitalization mistakes:

- **Accidental CapsLock** — corrects text typed with CapsLock enabled.
- **Double Capital Letters** — corrects words beginning with two consecutive uppercase letters.

Corrections are performed using language-specific dictionaries. Latin abbreviations and unrecognized words are left unchanged.

### Transliteration

Converts selected Cyrillic text into Latin characters.

Supported languages:
- Ukrainian
- Russian

The application automatically determines the source language based on language-specific characters.

## Application Interface

<img width="795" height="628" alt="image" src="https://github.com/user-attachments/assets/2d7fa3ae-54fb-46f0-a3a2-60972a1d7a3b" />

*Main settings interface with application configuration and correction options.*

## Hotkeys

| Shortcut | Action |
|:---|:---|
| `Pause` | Correct the last word or undo the most recent automatic correction. |
| `Shift + Pause` | Convert selected text to another keyboard layout. |
| `Ctrl + Alt + T` | Transliterate selected text (Cyrillic → Latin). |
| `Esc` | Close the settings window or cancel hotkey assignment. |

Hotkeys can be customized in the **Keys** section of the application settings.

The default transliteration shortcut is `Ctrl + Alt + T`.

## Automatic Correction

To minimize incorrect conversions, the application uses several contextual rules:

- Considers up to two preceding words.
- Processes words within the same line and keyboard layout.
- Requires words to be separated by a single space.
- Checks words against the target language dictionary.
- Leaves unrecognized words, proper nouns, and rare terms unchanged.

The `Pause` key provides manual correction when automatic detection does not produce a result.

## Operational Details

### Undoing Corrections

Pressing `Pause` immediately after an automatic correction restores the original text.

If another character has been entered after the correction, `Pause` performs a standard manual correction instead.

### Application Exclusions

Applications added to the exclusion list do not support:

- `Shift + Pause` — selected text layout conversion.
- Transliteration.

Manual correction using `Pause` remains available, including in excluded applications such as terminal emulators.

### Transliteration Rules

Transliteration works exclusively in one direction:

**Cyrillic → Latin**

Language detection is based on specific characters of the Ukrainian and Russian alphabets.

If the source language cannot be determined unambiguously, Ukrainian transliteration is used by default.

### CapsLock Behavior

Automatic case correction is triggered by pressing the `Spacebar`.

Words completed using `Enter` or `Tab` are not automatically corrected.

## Settings and Logging

Application settings are organized into the following sections:

| Section | Description |
|:---|:---|
| **General** | Core application settings and startup options. |
| **Corrections** | Automatic layout correction, self-learning, and capitalization rules. |
| **Keys** | Custom hotkey configuration. |
| **Dictionary** | Managing custom user-defined words. |

The application also maintains an event log that records important operations, such as the number of automatically corrected words.

**Privacy:** The event log does not store the actual text entered by the user.

## System Integration

AUREON Keyboard Switcher runs in the background and is accessible through the Windows system tray.

System notifications inform users about important events, including successful addition of new words to the user dictionary.

![AUREON Keyboard Switcher — System Tray](https://github.com/user-attachments/assets/99541de1-62c3-48a7-bf3b-e296b210d8f3)

*System tray integration and application context menu.*

---

## About

**AUREON Keyboard Switcher** combines automatic keyboard layout correction, intelligent capitalization, a self-learning dictionary, and transliteration in a single lightweight Windows application.

Its goal is simple: make everyday text input faster, more convenient, and less error-prone.

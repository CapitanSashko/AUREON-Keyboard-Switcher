AUREON Keyboard Switcher
AUREON Keyboard Switcher is a lightweight Windows background application designed to automatically fix text typed in the wrong keyboard layout. It also manages letter casing, supports Cyrillic-to-Latin transliteration, and features a self-learning user dictionary.

1. Purpose
The application runs in the background (system tray) and allows users to correct text either automatically (upon pressing the spacebar) or manually using hotkeys. It is built to increase typing speed and comfort by removing the need to manually delete and retype words when the wrong keyboard layout is active.

2. Key Features
Automatic Layout Correction
The program detects when a word is typed in the wrong layout and automatically converts it to the correct language. If the correction is unnecessary, it can be undone or performed manually using the Pause key.

Self-Learning
If a user cancels an automatic correction by pressing the Pause key, the program can add the original word to a custom user dictionary. Future occurrences of this word will not be automatically corrected.
Self-learning can be disabled in the settings. The custom dictionary is stored in the settings.json file under the UserWords field and can be easily transferred to other computers.

Case Correction
The program automatically handles common typing mistakes related to letter casing:

Accidental CapsLock activation.

Double capital letters at the beginning of a word.
Corrections are made based on the active language and dictionary. Latin abbreviations and unrecognized words remain untouched.

Transliteration
Converts selected Cyrillic text into Latin characters. Ukrainian and Russian transliterations are supported with automatic language detection.

[INSERT SCREENSHOT: Main Settings Window]
(Hint: Screenshot showing the general settings with toggles for auto-correction and autostart)

3. Hotkeys
Shortcut	Action
Pause	Fix the last word or undo the last automatic correction.
Shift + Pause	Convert selected text to another layout.
Ctrl + Alt + T	Transliterate selected text.
Esc	Close the settings window or cancel hotkey assignment.
Hotkeys can be changed in the "Keys" (Клавіші) tab in the program settings. By default, Ctrl + Alt + T is used for transliteration.

4. How Automatic Correction Works
To prevent false positives, the program uses strict conditions:

Considers up to two preceding words.

Corrects words within a single line and the same layout.

Requires a single space between words.

Checks the dictionary of the target language.

Proper nouns and rare words not present in the dictionary are not automatically corrected.
The Pause key allows manual correction for such cases.

5. Operational Details
Undoing a Correction
If you press Pause immediately after an automatic correction, the program will revert to the original text. If a new character has already been typed after the correction, Pause will perform a standard manual correction instead.

Exclusions
Programs added to the exclusion list do not support automatic actions like Shift + Pause and transliteration. However, manual correction using the Pause key is available even in excluded programs (e.g., terminal emulators).

Transliteration Rules
Works only in the Cyrillic → Latin direction. Language detection is based on specific letters of the Ukrainian and Russian alphabets. If the language cannot be unambiguously determined, the Ukrainian variant is used by default.

CapsLock
Automatic case correction triggers when the Spacebar is pressed. Finishing a word with Enter or Tab does not trigger the correction.

6. Settings and Logging
The application settings are divided into main sections:

General — core application parameters.

Corrections — management of auto-correction, self-learning, and case rules.

Keys — hotkey configuration.

Dictionary — view, add, and delete custom user words.

<img width="786" height="617" alt="Знімок екрана 2026-11-08 145313" src="https://github.com/user-attachments/assets/aa5306c4-86da-4c2f-ae1b-0af5c734f11c" />

(Hint: Screenshot showing the correction toggles or the user dictionary tab)

The event log records the main actions of the program (e.g., the number of automatically corrected words) without saving the actual text you type, ensuring privacy.

7. System Integration
The program runs in the background and is accessible via the Windows system tray. Notifications inform the user about important events, such as a new word being added to the user dictionary.

<img width="249" height="98" alt="file (1)" src="https://github.com/user-attachments/assets/99541de1-62c3-48a7-bf3b-e296b210d8f3" />

(Hint: Screenshot showing the right-click context menu on the system tray icon)

AUREON Keyboard Switcher is a tool designed to improve text input speed and convenience, combining automatic layout correction, case management, a user dictionary, and transliteration into a single application.

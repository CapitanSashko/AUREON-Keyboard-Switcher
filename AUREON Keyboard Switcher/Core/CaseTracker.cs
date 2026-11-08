namespace AUREON_Keyboard_Switcher.Core
{
    /// <summary>Яку помилку регістру зроблено в слові.</summary>
    public enum CaseMistake
    {
        /// <summary>Помилки немає.</summary>
        None,

        /// <summary>Випадково ввімкнений CapsLock: «пРИВІТ» замість «Привіт».</summary>
        CapsLockAccident,

        /// <summary>Подвійна велика літера на початку: «ПРивіт» замість «Привіт».</summary>
        DoubleCapital
    }

    /// <summary>
    /// Відстежує, як натискалися літерні клавіші слова (Shift і CapsLock).
    /// Помилка визначається за способом натискання, а не за виглядом слова,
    /// тому клавіші ж, х, ї, є, ö, ä тощо не заважають розпізнаванню.
    /// </summary>
    public sealed class CaseTracker
    {
        private int _letters;
        private bool _capsAccident = true;
        private bool _doubleCapital = true;

        /// <summary>Скільки літерних клавіш натиснуто в поточному слові.</summary>
        public int LetterKeys => _letters;

        /// <summary>Нове слово: починаємо спочатку.</summary>
        public void Reset()
        {
            _letters = 0;
            _capsAccident = true;
            _doubleCapital = true;
        }

        /// <summary>Викликати для кожної літерної клавіші слова.</summary>
        public void AddLetterKey(bool shift, bool capsLock)
        {
            _letters++;

            // Випадковий CapsLock: він увімкнений, а Shift тиснули лише на першій літері
            if (!capsLock || shift != (_letters == 1))
                _capsAccident = false;

            // Подвійна велика: CapsLock вимкнений, Shift на перших двох літерах, далі без нього
            if (capsLock || shift != (_letters <= 2))
                _doubleCapital = false;
        }

        /// <summary>Яку помилку регістру зроблено в щойно набраному слові.</summary>
        public CaseMistake Detect(bool includeDoubleCapital)
        {
            if (_letters < 3)
                return CaseMistake.None;

            if (_capsAccident)
                return CaseMistake.CapsLockAccident;

            if (includeDoubleCapital && _letters >= 4 && _doubleCapital)
                return CaseMistake.DoubleCapital;

            return CaseMistake.None;
        }
    }
}
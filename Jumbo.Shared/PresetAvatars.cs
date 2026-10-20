namespace Jumbo.Shared;

public static class PresetAvatars
{
    public static readonly IReadOnlyList<string> All = new List<string>
    {
        // 1. Boy — brown hair, blue shirt
        "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='24' fill='#E3F2FD'/><path d='M22 40 Q50 6 78 40 L78 46 Q50 30 22 46 Z' fill='#6D4C41'/><circle cx='50' cy='42' r='20' fill='#FFCCBC'/><circle cx='42' cy='40' r='3' fill='#3E2723'/><circle cx='58' cy='40' r='3' fill='#3E2723'/><path d='M42 50 Q50 56 58 50' stroke='#3E2723' stroke-width='2.5' fill='none' stroke-linecap='round'/><path d='M14 92 Q50 62 86 92 Z' fill='#42A5F5'/></svg>",

        // 2. Girl — pigtails, pink shirt
        "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='24' fill='#FCE4EC'/><circle cx='20' cy='46' r='9' fill='#4E342E'/><circle cx='80' cy='46' r='9' fill='#4E342E'/><path d='M24 34 Q50 10 76 34 L74 44 Q50 26 26 44 Z' fill='#4E342E'/><circle cx='50' cy='44' r='20' fill='#FFE0B2'/><circle cx='43' cy='42' r='3' fill='#3E2723'/><circle cx='57' cy='42' r='3' fill='#3E2723'/><path d='M43 52 Q50 57 57 52' stroke='#3E2723' stroke-width='2.5' fill='none' stroke-linecap='round'/><path d='M14 92 Q50 64 86 92 Z' fill='#EC407A'/></svg>",

        // 3. Boy — curly black hair, glasses, green shirt
        "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='24' fill='#E8F5E9'/><path d='M20 42 Q18 10 50 10 Q82 10 80 42 Q68 26 50 30 Q32 26 20 42 Z' fill='#212121'/><circle cx='50' cy='44' r='20' fill='#D7A379'/><circle cx='42' cy='43' r='8' fill='none' stroke='#212121' stroke-width='2.5'/><circle cx='58' cy='43' r='8' fill='none' stroke='#212121' stroke-width='2.5'/><path d='M44 54 Q50 58 56 54' stroke='#3E2723' stroke-width='2.5' fill='none' stroke-linecap='round'/><path d='M14 92 Q50 62 86 92 Z' fill='#66BB6A'/></svg>",

        // 4. Girl — blonde straight hair, purple shirt
        "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='24' fill='#F3E5F5'/><path d='M16 60 Q14 14 50 12 Q86 14 84 60 L74 56 Q76 24 50 22 Q24 24 26 56 Z' fill='#FFD54F'/><circle cx='50' cy='44' r='20' fill='#FFE0B2'/><circle cx='43' cy='42' r='3' fill='#3E2723'/><circle cx='57' cy='42' r='3' fill='#3E2723'/><path d='M43 52 Q50 57 57 52' stroke='#3E2723' stroke-width='2.5' fill='none' stroke-linecap='round'/><path d='M14 92 Q50 64 86 92 Z' fill='#AB47BC'/></svg>",

        // 5. Robot
        "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='24' fill='#ECEFF1'/><rect x='24' y='30' width='52' height='40' rx='10' fill='#90A4AE'/><circle cx='50' cy='18' r='5' fill='#78909C'/><rect x='48' y='10' width='4' height='10' fill='#78909C'/><circle cx='38' cy='48' r='6' fill='#00E5FF'/><circle cx='62' cy='48' r='6' fill='#00E5FF'/><rect x='38' y='60' width='24' height='5' rx='2' fill='#37474F'/></svg>",

        // 6. Cat
        "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='24' fill='#FFF3E0'/><path d='M26 34 L14 14 L38 28 Z' fill='#FFA726'/><path d='M74 34 L86 14 L62 28 Z' fill='#FFA726'/><circle cx='50' cy='46' r='24' fill='#FFB74D'/><circle cx='41' cy='42' r='3.5' fill='#3E2723'/><circle cx='59' cy='42' r='3.5' fill='#3E2723'/><path d='M50 48 L46 53 L54 53 Z' fill='#3E2723'/><path d='M30 56 H44 M30 60 H42' stroke='#3E2723' stroke-width='1.5'/><path d='M70 56 H56 M70 60 H58' stroke='#3E2723' stroke-width='1.5'/></svg>",

        // 7. Fox
        "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='24' fill='#FFF8E1'/><path d='M24 30 L12 8 L36 26 Z' fill='#FB8C00'/><path d='M76 30 L88 8 L64 26 Z' fill='#FB8C00'/><path d='M24 30 L18 14 L34 24 Z' fill='#FFF3E0'/><path d='M76 30 L82 14 L66 24 Z' fill='#FFF3E0'/><circle cx='50' cy='48' r='24' fill='#FB8C00'/><path d='M50 48 m-14,10 a14,10 0 0 0 28,0 Z' fill='#FFF3E0'/><circle cx='42' cy='44' r='3.5' fill='#3E2723'/><circle cx='58' cy='44' r='3.5' fill='#3E2723'/><path d='M50 52 L46 57 L54 57 Z' fill='#3E2723'/></svg>",

        // 8. Bear
        "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='24' fill='#EFEBE9'/><circle cx='24' cy='24' r='11' fill='#8D6E63'/><circle cx='76' cy='24' r='11' fill='#8D6E63'/><circle cx='50' cy='48' r='26' fill='#A1887F'/><circle cx='50' cy='54' r='12' fill='#D7CCC8'/><circle cx='41' cy='42' r='3.5' fill='#3E2723'/><circle cx='59' cy='42' r='3.5' fill='#3E2723'/><circle cx='50' cy='52' r='3.5' fill='#3E2723'/></svg>",

        // 9. Astronaut
        "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='24' fill='#E1F5FE'/><circle cx='50' cy='48' r='30' fill='#ECEFF1'/><circle cx='50' cy='48' r='22' fill='#0288D1'/><circle cx='44' cy='44' r='6' fill='#FFFFFF' opacity='0.6'/><rect x='34' y='72' width='32' height='10' rx='5' fill='#B0BEC5'/></svg>",

        // 10. Superhero
        "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='24' fill='#E8EAF6'/><path d='M20 92 Q10 50 30 30 L70 30 Q90 50 80 92 Z' fill='#5C6BC0'/><circle cx='50' cy='42' r='19' fill='#FFCCBC'/><path d='M31 38 Q50 26 69 38 L69 42 Q50 34 31 42 Z' fill='#3E2723'/><rect x='30' y='38' width='40' height='9' rx='4' fill='#212121'/><circle cx='40' cy='42' r='3' fill='#FFCCBC'/><circle cx='60' cy='42' r='3' fill='#FFCCBC'/></svg>",

        // 11. Unicorn
        "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='24' fill='#F3E5F5'/><path d='M50 8 L56 30 L44 30 Z' fill='#FFD54F'/><path d='M20 34 Q14 16 30 20 Q26 30 34 36 Z' fill='#F06292'/><path d='M30 40 Q40 18 60 22 Q80 28 74 50 Q80 66 66 78 Q50 86 36 76 Q24 66 26 52 Z' fill='#FFFFFF'/><circle cx='42' cy='50' r='3.5' fill='#3E2723'/><circle cx='58' cy='50' r='3.5' fill='#3E2723'/><path d='M46 60 Q50 63 54 60' stroke='#F06292' stroke-width='2.5' fill='none' stroke-linecap='round'/></svg>",

        // 12. Alien
        "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><rect x='4' y='4' width='92' height='92' rx='24' fill='#E0F7FA'/><path d='M50 14 Q78 20 74 52 Q70 84 50 88 Q30 84 26 52 Q22 20 50 14 Z' fill='#66BB6A'/><ellipse cx='38' cy='48' rx='9' ry='13' fill='#212121'/><ellipse cx='62' cy='48' rx='9' ry='13' fill='#212121'/><ellipse cx='41' cy='44' rx='3' ry='4' fill='#fff'/><ellipse cx='65' cy='44' rx='3' ry='4' fill='#fff'/><path d='M42 68 Q50 74 58 68' stroke='#1B5E20' stroke-width='2.5' fill='none' stroke-linecap='round'/></svg>",
    };
}

using System;
using System.Collections.Generic;
using Fields.Core;
using UnityEngine;

namespace Fields.World
{
    /// <summary>
    /// BARNYARD A.I. v0.1 — keyword-based chatbot for the vintage computer terminal.
    /// All responses come from the active LocalizationManager table (ai.* keys).
    /// The terminal is an ASCII-only retro interface, so non-Latin locales
    /// intentionally keep responses in English.
    /// </summary>
    public static class BarnAI
    {
        // ── Command result ───────────────────────────────────────────────── //

        public readonly struct Response
        {
            public readonly string Text;
            public readonly string AchievementId;   // null = no achievement
            public readonly bool   ExitAiMode;      // true = switch back to cheat mode

            public Response(string text, string ach = null, bool exit = false)
            {
                Text          = text;
                AchievementId = ach;
                ExitAiMode    = exit;
            }
        }

        // ── Keyword rules: first match wins ──────────────────────────────── //

        static readonly (string[] kw, string locKey, string ach)[] _rules =
        {
            (new[] {"help","?","commands","menu","list"}, "ai.help", null),
            (new[] {"info","about","game","version","studio","developer"}, "ai.info", null),
            (new[] {"back","exit","cheat","quit","bye","goodbye","close"}, null, null),   // null locKey = exit

            (new[] {"hello","hi","hey","howdy","sup","greet","morning"}, "ai.hello", null),

            // Film / game references
            (new[] {"matrix","red pill","blue pill","neo","morpheus","zion"}, "ai.matrix", null),
            (new[] {"jedi","sith","lightsaber","star wars","luke","vader","force","may the"}, "ai.force", null),
            (new[] {"freeman","gordon","hl2","half life","combine","valve","gabe","crowbar"}, "ai.gordon", null),
            (new[] {"terminator","skynet","arnold","hasta la vista","ill be back","t800"}, "ai.terminator", null),
            (new[] {"minecraft","creeper","steve","enderman","diamond","pickaxe","punching"}, "ai.minecraft", null),
            (new[] {"portal","glados","cake","aperture","companion cube","chell","still alive"}, "ai.portal", null),
            (new[] {"doom","rip and tear","doomguy","demons","bfg","shotgun","iddqd","idkfa"}, "ai.doom", null),
            (new[] {"gta","san andreas","cj","grove street","los santos","wanted level"}, "ai.gta", null),
            (new[] {"sims","woohoo","moodlet","plumbob","motherlode","rosebud"}, "ai.sims", null),
            (new[] {"witcher","geralt","toss a coin","ciri","yennefer","nilfgaard"}, "ai.witcher", null),
            (new[] {"among us","impostor","sus","vent","emergency meeting","crewmate"}, "ai.among", null),
            (new[] {"shrek","ogre","layers","onion","fiona","donkey","far far away"}, "ai.shrek", null),
            (new[] {"ramsay","gordon ramsay","lamb sauce","hell kitchen","chef","cook","raw"}, "ai.ramsay", null),

            // Philosophical / existential
            (new[] {"meaning","purpose","why","42","life","exist","universe"}, "ai.meaning", null),
            (new[] {"sentient","conscious","alive","robot","feel","emotion","think","aware"}, "ai.sentient", null),
            (new[] {"love","romance","date","heart","crush","feelings","marry"}, "ai.love", null),
            (new[] {"death","die","dead","kill","mortal","afterlife","heaven","soul"}, "ai.death", null),
            (new[] {"god","religion","pray","faith","church","divine","sin"}, "ai.god", null),

            // Farming / game-relevant
            (new[] {"money","rich","earn","income","salary","cash","dollar","profit"}, "ai.money", null),
            (new[] {"bale","hay","mow","grass","cut","scythe","field","harvest"}, "ai.bale", null),
            (new[] {"cat","kitten","meow","feline","kitty","purr"}, "ai.cat", null),
            (new[] {"chicken","hen","rooster","egg","cluck","poultry"}, "ai.chicken", null),
            (new[] {"weather","rain","sun","forecast","temperature","cloud","wind","storm"}, "ai.weather", null),
            (new[] {"secret","easter egg","hidden","treasure","find","discover"}, "ai.secret", null),

            // Meta / game
            (new[] {"error","bug","crash","fix","issue","problem","broken","glitch"}, "ai.error", null),
            (new[] {"update","patch","version","changelog","developer","dev","studio"}, "ai.update", null),
            (new[] {"friend","lonely","alone","coop","multiplayer","together","team"}, "ai.friends", null),
            (new[] {"tax","irs","government","audit","legal","accountant","form"}, "ai.tax", null),
            (new[] {"work","job","shift","task","todo","overtime","boss","deadline"}, "ai.work", null),
            (new[] {"doctor","health","sick","medicine","hospital","diagnos","symptom"}, "ai.doctor", null),
            (new[] {"banana","monkey","fruit","potassium"}, "ai.banana", null),
            (new[] {"pizza","food","hungry","eat","burger","sandwich","snack","fridge"}, "ai.pizza", null),
            (new[] {"time","clock","hour","minute","late","early","when"}, "ai.time", null),
            (new[] {"xyzzy","plugh","plover","colossal cave","zork","adventure"}, "ai.xyzzy", null),

            // Secret: Konami code typed as text
            (new[] {"uuddlrlrba","konami"}, "ai.konami", SteamManager.Achievements.KONAMI_CODE),
        };

        // ── Public API ───────────────────────────────────────────────────── //

        public static Response Query(string raw)
        {
            string lower = raw.Trim().ToLower();
            var loc       = LocalizationManager.Instance;

            foreach (var (kw, locKey, ach) in _rules)
            {
                if (Array.Exists(kw, k => lower.Contains(k)))
                {
                    if (locKey == null)           // exit command
                        return new Response(Loc(loc, "ai.exit"), exit: true);

                    if (locKey == "ai.help")
                        return new Response(BuildHelp(loc));

                    return new Response(Loc(loc, locKey), ach);
                }
            }

            return new Response(null);  // caller handles fallback escalation
        }

        // ── Fallback messages (escalating) ────────────────────────────────── //

        static readonly string[] _fallbackKeys =
        {
            "ai.fallback.0", "ai.fallback.1", "ai.fallback.2",
            "ai.fallback.3", "ai.fallback.4"
        };

        public static string GetFallback(int failCount, string input)
        {
            var loc = LocalizationManager.Instance;
            int idx  = Mathf.Min(failCount, _fallbackKeys.Length - 1);
            return string.Format(Loc(loc, _fallbackKeys[idx]), input.ToUpper());
        }

        // ── Help text ────────────────────────────────────────────────────── //

        static string BuildHelp(LocalizationManager loc) => Loc(loc, "ai.help");

        // ── Loc helper ───────────────────────────────────────────────────── //

        static string Loc(LocalizationManager loc, string key) =>
            loc != null ? loc.Get(key) : key;
    }
}

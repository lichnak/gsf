﻿/**************************************************************************\
   Copyright (c) 2008 - Gbtc, James Ritchie Carroll
   All rights reserved.
  
   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:
  
      * Redistributions of source code must retain the above copyright
        notice, this list of conditions and the following disclaimer.
       
      * Redistributions in binary form must reproduce the above
        copyright notice, this list of conditions and the following
        disclaimer in the documentation and/or other materials provided
        with the distribution.
  
   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER "AS IS" AND ANY
   EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
   IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
   PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
   CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY
   OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
   (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
   OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
  
\**************************************************************************/

using System;
using System.Reflection;

namespace System.Media.Music
{
    /// <summary>
    /// Provides a function signature for methods that produce an amplitude representing the
    /// acoustic pressure of a represented musical timbre for the given time.
    /// </summary>
    /// <param name="frequency">Fundamental frequency of the desired note in Hz.</param>
    /// <param name="time">Time in seconds.</param>
    /// <returns>The amplitude of the represented musical timbre at the given time.</returns>
    public delegate double TimbreFunction(double frequency, double time);

    /// <summary>
    /// Provides a function signature for methods that damp an amplitude representing a
    /// lowering of the acoustic pressure over time.
    /// </summary>
    /// <param name="sample">Sample index (0 to <paramref name="samplePeriod"/> - 1).</param>
    /// <param name="samplePeriod">Total period, in whole samples per second (i.e., time * SamplesPerSecond), over which to perform damping.</param>
    /// <returns>Scaling factor used to damp an amplitude at the given sample index.</returns>
    public delegate double DampingFunction(long sample, long samplePeriod);

    /// <summary>
    /// Defines fundamental musical note frequencies and methods to create them.
    /// </summary>
    /// <example>
    /// This example creates an in-memory wave file and adds notes to create a basic musical scale:
    /// <code>
    /// using System;
    /// using System.Media;
    /// using System.Media.Music;
    ///
    /// static class Program
    /// {
    ///     static void Main()
    ///     {
    ///         WaveFile waveFile = new WaveFile(SampleRate.Hz8000, BitsPerSample.Bits16, DataChannels.Mono);
    ///         TimbreFunction timbre = Note.BasicNote;             // Set the musical timbre
    ///         double amplitude = 0.25D * short.MaxValue;          // Set volume to 25% of maximum
    ///         double seconds = 6.0D;                              // Set length of wave file in seconds
    ///         double samplesPerSecond = waveFile.SampleRate;      // Gets the defined sample rate
    ///         double samplePeriod = seconds * samplesPerSecond;   // Compute total sample period
    ///         int totalNotes = 15;                                // Total notes to traverse
    ///         string noteID = Note.MiddleC;                       // Start note at middle C
    ///         double frequency = Note.GetNoteFrequency(noteID);   // Get frequency for middle C
    ///         double time;                                        // Time index
    ///         bool reverse = false;                               // Traverse notes in reverse order
    ///
    ///         for (int sample = 0; sample <![CDATA[<]]> samplePeriod; sample++)
    ///         {
    ///             // Change notes at even intervals within the sample period
    ///             if (sample > 0 <![CDATA[&&]]> (sample % (samplePeriod / totalNotes)) == 0)
    ///             {
    ///                 if (reverse)
    ///                 {
    ///                     noteID = Note.GetPreviousNoteID(noteID, false);
    ///                     frequency = Note.GetNoteFrequency(noteID);
    ///                 }
    ///                 else
    ///                 {
    ///                     noteID = Note.GetNextNoteID(noteID, false);
    ///                     frequency = Note.GetNoteFrequency(noteID);
    ///                 }
    ///
    ///                 // Go back down the scale after C5
    ///                 if (noteID == "C5")
    ///                     reverse = true;
    ///             }
    ///
    ///             // Compute time index of the current sample
    ///             time = sample / samplesPerSecond;
    ///
    ///             waveFile.AddBlock((short)(timbre(frequency, time) * amplitude));
    ///         }
    ///
    ///         waveFile.Play();
    ///         Console.ReadKey();
    ///     }
    /// }
    /// </code>
    /// </example>
    public class Note : IEquatable<Note>, IComparable<Note>, IComparable
    {
        #region [ Members ]

        // Constants

        // Fundamental musical note frequencies (http://www.phy.mtu.edu/~suits/notefreqs.html)
        public const double C0 = 16.35;
        public const double C0S = 17.32;
        public const double D0 = 18.35;
        public const double D0S = 19.45;
        public const double E0 = 20.6;
        public const double F0 = 21.83;
        public const double F0S = 23.12;
        public const double G0 = 24.5;
        public const double G0S = 25.96;
        public const double A0 = 27.5;
        public const double A0S = 29.14;
        public const double B0 = 30.87;
        public const double C1 = 32.7;
        public const double C1S = 34.65;
        public const double D1 = 36.71;
        public const double D1S = 38.89;
        public const double E1 = 41.2;
        public const double F1 = 43.65;
        public const double F1S = 46.25;
        public const double G1 = 49.0;
        public const double G1S = 51.91;
        public const double A1 = 55.0;
        public const double A1S = 58.27;
        public const double B1 = 61.74;
        public const double C2 = 65.41;
        public const double C2S = 69.3;
        public const double D2 = 73.42;
        public const double D2S = 77.78;
        public const double E2 = 82.41;
        public const double F2 = 87.31;
        public const double F2S = 92.5;
        public const double G2 = 98.0;
        public const double G2S = 103.83;
        public const double A2 = 110.0;
        public const double A2S = 116.54;
        public const double B2 = 123.47;
        public const double C3 = 130.81;
        public const double C3S = 138.59;
        public const double D3 = 146.83;
        public const double D3S = 155.56;
        public const double E3 = 164.81;
        public const double F3 = 174.61;
        public const double F3S = 185.0;
        public const double G3 = 196.0;
        public const double G3S = 207.65;
        public const double A3 = 220.0;
        public const double A3S = 233.08;
        public const double B3 = 246.94;
        public const double C4 = 261.63;    // Middle C
        public const double C4S = 277.18;
        public const double D4 = 293.66;
        public const double D4S = 311.13;
        public const double E4 = 329.63;
        public const double F4 = 349.23;
        public const double F4S = 369.99;
        public const double G4 = 392.0;
        public const double G4S = 415.3;
        public const double A4 = 440.0;
        public const double A4S = 466.16;
        public const double B4 = 493.88;
        public const double C5 = 523.25;
        public const double C5S = 554.37;
        public const double D5 = 587.33;
        public const double D5S = 622.25;
        public const double E5 = 659.26;
        public const double F5 = 698.46;
        public const double F5S = 739.99;
        public const double G5 = 783.99;
        public const double G5S = 830.61;
        public const double A5 = 880.0;
        public const double A5S = 932.33;
        public const double B5 = 987.77;
        public const double C6 = 1046.5;
        public const double C6S = 1108.73;
        public const double D6 = 1174.66;
        public const double D6S = 1244.51;
        public const double E6 = 1318.51;
        public const double F6 = 1396.91;
        public const double F6S = 1479.98;
        public const double G6 = 1567.98;
        public const double G6S = 1661.22;
        public const double A6 = 1760.0;
        public const double A6S = 1864.66;
        public const double B6 = 1975.53;
        public const double C7 = 2093.0;
        public const double C7S = 2217.46;
        public const double D7 = 2349.32;
        public const double D7S = 2489.02;
        public const double E7 = 2637.02;
        public const double F7 = 2793.83;
        public const double F7S = 2959.96;
        public const double G7 = 3135.96;
        public const double G7S = 3322.44;
        public const double A7 = 3520.0;
        public const double A7S = 3729.31;
        public const double B7 = 3951.07;
        public const double C8 = 4186.01;
        public const double C8S = 4434.92;
        public const double D8 = 4698.64;
        public const double D8S = 4978.03;

        /// <summary>Note ID for "Middle C"</summary>
        public const string MiddleC = "C4";

        // Fields
        private TimbreFunction m_timbre;
        private DampingFunction m_damping;
        private double m_frequency;
        private double m_noteValueTime;
        private double m_startTime;
        private double m_endTime;
        private double m_dynamic;
        private string m_noteID;
        private int m_noteValue;
        private int m_dots;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new note of the specified frequency and length.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Default note will be rest (zero frequency), quarter note.
        /// </para>
        /// <para>
        /// It is expected that <see cref="Note"/> objects will be constructed using
        /// object intializers.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Create a 3/8th length , middle C note
        /// Note note1 = new Note { Frequency = Note.C4, NoteValue = NoteValue.Quarter, Dots = 1 };
        /// 
        /// // Create a 1/8th length, E above middle C note
        /// Note note2 = new Note { NoteID = "E4", NoteValueBritish = NoteValueBritish.Quaver };
        /// </code>
        /// </example>
        public Note()
        {
            m_dynamic = -1.0D;
        }

        #endregion

        #region [ Properties ]

        /// <summary>Gets or sets frequency of this note.</summary>
        public double Frequency
        {
            get
            {
                return m_frequency;
            }
            set
            {
                if (m_frequency != value)
                    m_noteID = null;

                m_frequency = value;
            }
        }

        /// <summary>Gets or sets note ID of the note.</summary>
        /// <exception cref="ArgumentNullException">noteID is null.</exception>
        /// <exception cref="ArgumentException">Invalid note ID format - expected "Note + Octave + S?" (e.g., A2 or C5S).</exception>
        public string NoteID
        {
            get
            {
                return ToString();
            }
            set
            {
                m_frequency = GetNoteFrequency(value);
                m_noteID = value;
            }
        }

        /// <summary>Get or sets the note value, expressed in American form, representing the length of the note.</summary>
        public NoteValue NoteValue
        {
            get
            {
                return (NoteValue)m_noteValue;
            }
            set
            {
                m_noteValue = (int)value;
            }
        }

        /// <summary>Get or sets the note value, expressed in British form, representing the length of the note.</summary>
        public NoteValueBritish NoteValueBritish
        {
            get
            {
                return (NoteValueBritish)m_noteValue;
            }
            set
            {
                m_noteValue = (int)value;
            }
        }

        /// <summary>Gets or sets the total dotted note length extensions that apply to this note.</summary>
        public int Dots
        {
            get
            {
                return m_dots;
            }
            set
            {
                m_dots = value;
            }
        }

        /// <summary>
        /// Gets or sets the individual tibre function used to synthesize the sounds
        /// for this note (i.e., the instrument). If this timbre function is not defined,
        /// the timbre of the song will be used for the note.
        /// </summary>
        /// <remarks>
        /// Set this value to null to use current timbre function of the song.
        /// </remarks>
        public TimbreFunction Timbre
        {
            get
            {
                return m_timbre;
            }
            set
            {
                m_timbre = value;
            }
        }

        /// <summary>
        /// Gets or sets the individual damping function used to lower the sound volume
        /// for this note over time. If this damping function is not defined, the
        /// damping algorithm of the song will be used for the note.
        /// </summary>
        public DampingFunction Damping
        {
            get
            {
                return m_damping;
            }
            set
            {
                m_damping = value;
            }
        }

        /// <summary>
        /// Gets or sets the dynamic (i.e., volume) for this note.  If the dynamic
        /// is undefined, the dynamic of the song will be used.
        /// </summary>
        /// <remarks>
        /// Set this value to undefined to use the current dynamic of the song.
        /// </remarks>
        public Dynamic Dynamic
        {
            get
            {
                if (m_dynamic == -1.0D)
                    return Dynamic.Undefined;

                // Dynamic can be custom, so return closest match...
                int dynamic = (int)m_dynamic * 100;

                if (dynamic <= (int)Dynamic.Pianissimo)
                {
                    return Dynamic.Pianissimo;
                }
                else if (dynamic <= (int)Dynamic.Piano)
                {
                    return Dynamic.Piano;
                }
                else if (dynamic <= (int)Dynamic.MezzoPiano)
                {
                    return Dynamic.MezzoPiano;
                }
                else if (dynamic <= (int)Dynamic.MezzoForte)
                {
                    return Dynamic.MezzoForte;
                }
                else if (dynamic <= (int)Dynamic.Forte)
                {
                    return Dynamic.Forte;
                }
                else
                {
                    return Dynamic.Fortissimo;
                }
            }
            set
            {
                if (value == Dynamic.Undefined)
                    m_dynamic = -1.0D;
                else
                    m_dynamic = (double)value / 100.0D;
            }
        }

        /// <summary>
        /// Gets or sets a custom dynamic (i.e., volume) expressed as percentage
        /// in the range of 0 to 1 for this note.   If the dynamic is set to -1,
        /// the dynamic of the song will be used.
        /// </summary>
        /// <remarks>
        /// Set this value to -1 to use the current dynamic of the song.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Value must be expressed as a fractional percentage between zero and one.
        /// </exception>
        public double CustomDynamic
        {
            get
            {
                return m_dynamic;
            }
            set
            {
                if (value != -1.0D && (value < 0.0D || value > 1.0D))
                    throw new ArgumentOutOfRangeException("CustomDynamic", "Value must be expressed as a fractional percentage between zero and one.");

                m_dynamic = value;
            }
        }

        /// <summary>
        /// Returns the relative note duration.
        /// </summary>
        /// <returns>Relative note duration.</returns>
        public double Duration
        {
            get
            {
                return NoteValue.Duration(m_dots);
            }
        }

        /// <summary>
        /// Gets cached note value time calculated from a call to <see cref="CalculateNoteValueTime"/>.
        /// </summary>
        public double NoteValueTime
        {
            get
            {
                return m_noteValueTime;
            }
        }

        /// <summary>Gets or sets start time index for this note.</summary>
        public double StartTime
        {
            get
            {
                return m_startTime;
            }
            set
            {
                m_startTime = value;
            }
        }

        /// <summary>Gets or sets end time index for this note.</summary>
        public double EndTime
        {
            get
            {
                return m_endTime;
            }
            set
            {
                m_endTime = value;
            }
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Calculates the actual time duration, in seconds, for the specified tempo that
        /// the note value will last. For example, if tempo is M.M. 120 quarter-notes per
        /// minte, then each quarter-note would last a half-second.
        /// </summary>
        /// <param name="tempo">Tempo used to calculate note value time.</param>
        /// <returns>Calculated note value time.</returns>
        /// <remarks>
        /// Calculated value is cached and available from <see cref="NoteValueTime"/> property.
        /// </remarks>
        public double CalculateNoteValueTime(Tempo tempo)
        {
            m_noteValueTime = tempo.CalculateNoteValueTime((NoteValue)m_noteValue, m_dots);
            return m_noteValueTime;
        }
        
        public override string ToString()
        {
            if (m_noteID == null && m_frequency > 0.0D)
            {
                // Attempt to look up note ID
                foreach (FieldInfo field in typeof(Note).GetFields())
                {
                    if (m_frequency == (double)field.GetRawConstantValue())
                    {
                        m_noteID = field.Name;
                        break;
                    }
                }

                // If no note ID was found for given frequency, just assign the
                // frequency as the note ID
                if (m_noteID == null)
                    m_noteID = m_frequency.ToString();
            }

            return m_noteID;
        }

        /// <summary>Returns True if the frequency and value of this note equals the frequency and value of the specified other note.</summary>
        public bool Equals(Note other)
        {
            return (CompareTo(other) == 0);
        }

        /// <summary>Returns True if the frequency and value of this note equals the frequency and value of the specified other note.</summary>
        public override bool Equals(object obj)
        {
            Note other = obj as Note;
            if (other != null) return Equals(other);
            throw new ArgumentException("Object is not an Note", "obj");
        }

        /// <summary>Notes are compared by frequency, then by value (i.e., duration).</summary>
        public int CompareTo(Note other)
        {
            int result = m_frequency.CompareTo(other.Frequency);

            if (result == 0)
                result = Duration.CompareTo(other.Duration);

            return result;
        }

        /// <summary>Notes are compared by frequency, then by value (i.e., duration).</summary>
        public int CompareTo(object obj)
        {
            Note other = obj as Note;
            if (other != null) return CompareTo(other);
            throw new ArgumentException("Note can only be compared with other Notes...");
        }

        public override int GetHashCode()
        {
            return (Frequency * Duration).GetHashCode();
        }

        #endregion

        #region [ Operators ]

        public static bool operator ==(Note note1, Note note2)
        {
            return note1.Equals(note2);
        }

        public static bool operator !=(Note note1, Note note2)
        {
            return !note1.Equals(note2);
        }

        public static bool operator >(Note note1, Note note2)
        {
            return note1.CompareTo(note2) > 0;
        }

        public static bool operator >=(Note note1, Note note2)
        {
            return note1.CompareTo(note2) >= 0;
        }

        public static bool operator <(Note note1, Note note2)
        {
            return note1.CompareTo(note2) < 0;
        }

        public static bool operator <=(Note note1, Note note2)
        {
            return note1.CompareTo(note2) <= 0;
        }

        #endregion

        #region [ Static ]

        // Static Methods

        /// <summary>
        /// Gets the specified note frequency.
        /// </summary>
        /// <param name="noteID">ID of the note to retrieve - expected format is "Note + Octave + S?" (e.g., A2 or C5S)</param>
        /// <returns>The specified note.</returns>
        /// <exception cref="ArgumentNullException">noteID is null.</exception>
        /// <exception cref="ArgumentException">Invalid note ID format - expected "Note + Octave + S?" (e.g., A2 or C5S).</exception>
        public static double GetNoteFrequency(string noteID)
        {
            noteID = ValidateNoteID(noteID);
            return GetNoteFrequency(noteID[0], int.Parse(noteID[1].ToString()), noteID.Length > 2 && noteID[2] == 'S' ? true : false);
        }

        /// <summary>
        /// Gets the specified note frequency.
        /// </summary>
        /// <param name="note">Note (A - G) to retrieve.</param>
        /// <param name="octave">Octave of the the note to retrieve (0 - 8).</param>
        /// <param name="sharp">Indicates to get the "sharp" version of the note.</param>
        /// <returns>The specified note.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Notes must be A - G, octaves must be 0 - 8, first note is C0, last note is D8S.</exception>
        /// <exception cref="ArgumentException">Sharps are not defined for notes 'B' and 'E'.</exception>
        public static double GetNoteFrequency(char note, int octave, bool sharp)
        {
            if (note < 'A' || note > 'G')
                throw new ArgumentOutOfRangeException("note", "Note must be A - G");

            if (octave < 0 || octave > 8)
                throw new ArgumentOutOfRangeException("ocatve", "Octave must be between 0 and 8");

            if (octave == 8 && (note < 'C' || note > 'D'))
                throw new ArgumentOutOfRangeException("note", "Maximum note defined for octave 8 is \'D#\'");

            if (sharp && (note == 'B' || note == 'E'))
                throw new ArgumentException("Sharps are not defined for notes \'B\' and \'E\'");

            return (double)typeof(Note).GetField(string.Format("{0}{1}{2}", note, octave, sharp ? "S" : "")).GetRawConstantValue();
        }

        /// <summary>
        /// Gets the next note ID in sequence after the specified note ID.
        /// </summary>
        /// <param name="noteID">ID of the current note - expected format is "Note + Octave + S?" (e.g., A2 or C5S)</param>
        /// <param name="includeSharps">Set to True to include sharp notes in the sequence.</param>
        /// <returns>The next note ID that is after the specified note ID.</returns>
        /// <exception cref="ArgumentNullException">noteID is null.</exception>
        /// <exception cref="ArgumentException">Invalid note ID format - expected "Note + Octave + S?" (e.g., A2 or C5S).</exception>
        public static string GetNextNoteID(string noteID, bool includeSharps)
        {
            noteID = ValidateNoteID(noteID);

            char note = noteID[0];
            int octave = int.Parse(noteID[1].ToString());
            bool sharp = (noteID.Length > 2 && noteID[2] == 'S' ? true : false);

            // Transition to next octave after each B note
            if (note == 'B')
                octave++;

            // Include sharp notes if requested
            if (includeSharps && !sharp && note != 'B' && note != 'E')
            {
                sharp = true;
            }
            else
            {
                sharp = false;

                // Transition to next note frequency
                if (note == 'G')
                    note = 'A';
                else
                    note++;
            }

            return string.Format("{0}{1}{2}", note, octave, sharp ? "S" : "");
        }

        /// <summary>
        /// Gets the previous note ID in sequence before the specified note ID.
        /// </summary>
        /// <param name="noteID">ID of the current note - expected format is "Note + Octave + S?" (e.g., A2 or C5S)</param>
        /// <param name="includeSharps">Set to True to include sharp notes in the sequence.</param>
        /// <returns>The previous note ID that is before the specified note ID.</returns>
        /// <exception cref="ArgumentNullException">noteID is null.</exception>
        /// <exception cref="ArgumentException">Invalid note ID format - expected "Note + Octave + S?" (e.g., A2 or C5S).</exception>
        public static string GetPreviousNoteID(string noteID, bool includeSharps)
        {
            noteID = ValidateNoteID(noteID);

            char note = noteID[0];
            int octave = int.Parse(noteID[1].ToString());
            bool transition = true, sharp = (noteID.Length > 2 && noteID[2] == 'S' ? true : false);

            // Transition to previous octave at each C note
            if (note == 'C' && !sharp)
                octave--;

            // Include sharp notes if requested
            if (includeSharps)
            {
                if (!sharp)
                {
                    if (note != 'C' && note != 'F')
                        sharp = true;
                    else
                        sharp = false;
                }
                else
                {
                    transition = false;
                    sharp = false;
                }
            }
            else
            {
                sharp = false;
            }

            if (transition)
            {
                // Transition to previous note frequency
                if (note == 'A')
                    note = 'G';
                else
                    note--;
            }

            return string.Format("{0}{1}{2}", note, octave, sharp ? "S" : "");
        }

        private static string ValidateNoteID(string noteID)
        {
            if (noteID == null)
                throw new ArgumentNullException("noteID");

            if (noteID.Length < 2)
                throw new ArgumentException("Invalid note ID format - expected \"Note + Octave + S?\" (e.g., A2 or C5S)");

            return noteID.ToUpper();
        }

        /// <summary>
        /// Computes the angular frequency for the given time.
        /// </summary>
        /// <param name="frequency">Frequency in Hz.</param>
        /// <param name="time">Time in seconds.</param>
        /// <returns>The computed angular frequency in radians per second at given time.</returns>
        public static double AngularFrequency(double frequency, double time)
        {
            // 2 PI f t
            //      f = Frequency (Hz)
            //      t = period    (Seconds)

            return (2 * Math.PI * frequency) * time;
        }

        // Timbre functions

        /// <summary>
        /// Generates a pure tone for the given frequency and time.
        /// </summary>
        /// <param name="frequency">Fundamental frequency of the desired note in Hz.</param>
        /// <param name="time">Time in seconds.</param>
        /// <returns>The amplitude for a pure tone at the given time.</returns>
        /// <remarks>
        /// This method computes an amplitude representing the acoustic pressure of a
        /// pure tone of the given frequency for the given time.
        /// </remarks>
        public static double PureTone(double frequency, double time)
        {
            return Math.Sin(AngularFrequency(frequency, time));
        }

        /// <summary>
        /// Generates a basic note for the given frequency and time.
        /// </summary>
        /// <param name="frequency">Fundamental frequency of the desired note in Hz.</param>
        /// <param name="time">Time in seconds.</param>
        /// <returns>The amplitude for a basic note at the given time.</returns>
        /// <remarks>
        /// This method computes an amplitude representing the acoustic pressure of a
        /// basic note of the given frequency for the given time.
        /// </remarks>
        public static double BasicNote(double frequency, double time)
        {
            double wt, r1, r2;

            wt = AngularFrequency(frequency, time);
            r1 = Math.Sin(wt) + 0.75 * Math.Sin(3 * wt);
            r2 = Math.Sin(wt);

            return r1 + r2;
        }

        /// <summary>
        /// Generates a simulated clarinet note for the given frequency and time.
        /// </summary>
        /// <param name="frequency">Fundamental frequency of the desired note in Hz.</param>
        /// <param name="time">Time in seconds.</param>
        /// <returns>The amplitude for a simulated clarinet note at the given time.</returns>
        /// <remarks>
        /// This method computes an amplitude representing the acoustic pressure of a
        /// simulated clarinet note of the given frequency for the given time.
        /// </remarks>
        public static double SimulatedClarinet(double frequency, double time)
        {
            double wt, r1;

            wt = AngularFrequency(frequency, time);

            // Simulated Clarinet equation
            // s(t) = sin(wt) + 0.75 *      sin(3 * wt) + 0.5 *      sin(5 * wt) + 0.14 *      sin(7 * wt) + 0.5 *      sin(9 * wt) + 0.12 *      sin(11 * wt) + 0.17 *      sin(13 * wt)
            r1 = Math.Sin(wt) + 0.75 * Math.Sin(3 * wt) + 0.5 * Math.Sin(5 * wt) + 0.14 * Math.Sin(7 * wt) + 0.5 * Math.Sin(9 * wt) + 0.12 * Math.Sin(11 * wt) + 0.17 * Math.Sin(13 * wt);

            return r1;
        }

        // Damping functions

        /// <summary>
        /// Produces a damping signature that represents no damping over time.
        /// </summary>
        /// <param name="sample">Sample index (0 to <paramref name="samplePeriod"/> - 1).</param>
        /// <param name="samplePeriod">Total period, in whole samples per second (i.e., time * SamplesPerSecond), over which to perform damping.</param>
        /// <returns>Returns a scalar of 1.0 regardless to time.</returns>
        /// <remarks>
        /// Zero damped sounds would be produced by synthetic sources such as an electronic keyboard.
        /// </remarks>
        public static double ZeroDamping(long sample, long samplePeriod)
        {
            return 1.0D;
        }

        /// <summary>
        /// Produces a natural damping curve similar to that of a piano - slowly damping over
        /// time until the key is released at which point the string is quickly damped.
        /// </summary>
        /// <param name="sample">Sample index (0 to <paramref name="samplePeriod"/> - 1).</param>
        /// <param name="samplePeriod">Total period, in whole samples per second (i.e., time * SamplesPerSecond), over which to perform damping.</param>
        /// <returns>Scaling factor used to damp an amplitude at the given time.</returns>
        public static double NaturalDamping(long sample, long samplePeriod)
        {
            return Math.Log10(samplePeriod - sample) / Math.Log10(samplePeriod);
        }

        #endregion
    }
}

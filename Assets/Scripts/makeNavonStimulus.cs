using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class makeNavonStimulus : MonoBehaviour
{
    /// <summary>
    /// script to procedurally generate a Navon stimulus in a  patch, 
    /// to be shown on the face of the screen object.
    /// </summary>

    int width = 1024;
    int height = 1024;
    public float offsetX, offsetY;
    Renderer rend;

    Texture2D currentTexture, nextTexture, maskTexture, fixationTexture;
    private Color[] pixelBuffer;
    private Texture2D[] preGenTextures;

    // (globalLetter, localLetter) for each StimulusType enum value (indices 0-11)
    private static readonly (char, char)[] typeLetters =
    {
        ('E', 'E'), // 0  E_BigE_LittleE
        ('E', 'I'), // 1  E_BigE_LittleI
        ('I', 'E'), // 2  E_BigI_LittleE
        ('F', 'F'), // 3  E_BigF_LittleF
        ('T', 'F'), // 4  E_BigT_LittleF
        ('F', 'T'), // 5  E_BigF_LittleT
        ('T', 'T'), // 6  T_BigT_LittleT
        ('T', 'E'), // 7  T_BigT_LittleE
        ('E', 'T'), // 8  T_BigE_LittleT
        ('F', 'F'), // 9  T_BigF_LittleF  (same pixels as type 3, different task context)
        ('I', 'F'), // 10 T_BigI_LittleF
        ('F', 'I'), // 11 T_BigF_LittleI
    };
    experimentParameters experimentParameters;
    public struct NavonParams
    {
        public float maskScale, px, py;
        public experimentParameters.StimulusType stimulusType;
        public experimentParameters.DetectionTask currentTask;
        public char globalLetter;
        public char localLetter;
        public bool targetPresent;   // Is the target letter (E or T) present?
        public bool isCongruent;     // Are global and local the same?
        public string trialCategory; // "Active" or "Inactive"
        public float targDuration;
    }

    public NavonParams navonP;
    public bool forceTargetPresent = false; // forces target present during standing still practice trials

    

    // Letter patterns for all 4 letters: E, T, F, I
    private int[,] letterE = new int[,] {
        {1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0},
        {1,0,0,0,0,0,0},
        {1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0},
        {1,0,0,0,0,0,0},
        {1,1,1,1,1,1,1}
    };

    private int[,] letterT = new int[,] {
        {1,1,1,1,1,1,1},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0}
    };

    private int[,] letterF = new int[,] {
        {1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0},
        {1,0,0,0,0,0,0},
        {1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0},
        {1,0,0,0,0,0,0},
        {1,0,0,0,0,0,0}
    };

    private int[,] letterI = new int[,] {
        {1,1,1,1,1,1,1},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {1,1,1,1,1,1,1}
    };

    // Hash mark pattern for masking - THIN VERSION (1 pixel lines)
    private int[,] hashMark = new int[,] {
        {0,1,0,0,0,1,0},  // Two thin vertical bars
        {0,1,0,0,0,1,0},
        {1,1,1,1,1,1,1},  // First horizontal bar
        {0,1,0,0,0,1,0},
        {1,1,1,1,1,1,1},  // Second horizontal bar
        {0,1,0,0,0,1,0},
        {0,1,0,0,0,1,0}   // Two thin vertical bars
    };

    [SerializeField]
    GameObject scriptHolder;
    void Start()
    {
        rend = GetComponent<Renderer>();
        experimentParameters = scriptHolder.GetComponent<experimentParameters>();
        offsetX = Random.Range(100f, 200f);
        offsetY = Random.Range(100f, 200f);

        // Default params - CENTERED ONLY
        navonP.maskScale = 60f;
        navonP.px = 0.5f;  // Always centered
        navonP.py = 0.5f;  // Always centered
        navonP.stimulusType = experimentParameters.StimulusType.E_BigE_LittleE;
        navonP.currentTask = experimentParameters.DetectionTask.DetectE;
        navonP.targetPresent = true;
        navonP.globalLetter = 'E';
        navonP.localLetter = 'E';
        navonP.isCongruent = true;
        navonP.trialCategory = "Active";
        navonP.targDuration =experimentParameters.targDurationsec;

        pixelBuffer = new Color[width * height];
        System.Array.Fill(pixelBuffer, Color.white);
        currentTexture = new Texture2D(width, height);
        currentTexture.SetPixels(pixelBuffer);
        currentTexture.Apply();

        fixationTexture = GenerateFixationCross();
        PreGenerateAllTextures();
        nextTexture = GenerateNavon(); // fast lookup after pre-generation
        showNavon();
        maskTexture = GenerateMask();
        
        Debug.Log("makeNavonStimulus initialized - DUAL DETECTION (E and T) with 12 stimulus types");
    }

    public void showNavon()
    {
        rend.material.mainTexture = nextTexture;
    }

    public void hideNavon()
    {
        rend.material.mainTexture = fixationTexture;
    }

    public void backwardMask()
    {
        // maskTexture is pre-generated once at Start() — reuse directly.
        rend.material.mainTexture = maskTexture;
    }

    public Texture2D GenerateNavon()
    {
        // Randomly select a stimulus type and set all navonP fields.
        // Then look up the pre-generated texture — no pixel work at all.
        if (navonP.currentTask == experimentParameters.DetectionTask.DetectE)
            GenerateStimulusForE();
        else
            GenerateStimulusForT();

        nextTexture = preGenTextures[(int)navonP.stimulusType];
        return nextTexture;
    }

    private void PreGenerateAllTextures()
    {
        preGenTextures = new Texture2D[12];
        for (int i = 0; i < preGenTextures.Length; i++)
        {
            (char g, char l) = typeLetters[i];
            preGenTextures[i] = CreateNavonTexture(g, l);
        }
        Debug.Log("Pre-generated 12 Navon textures.");
    }

    // Generates the pixel content for a single (globalLetter, localLetter) combination.
    // Called once per stimulus type at startup; result is cached in preGenTextures[].
    // White background is explicit because new Texture2D() initialises to transparent black.
    private Texture2D CreateNavonTexture(char globalLetter, char localLetter)
    {
        int[,] globalPattern = GetLetterPattern(globalLetter);
        int[,] localPattern  = GetLetterPattern(localLetter);

        const int globalLetterSize    = 400;
        const int localLetterSize     = 50;
        const int spacing             = 10;
        const int effectiveGlobalSize = globalLetterSize + spacing * 2; // 420

        float startX = width  / 2f - effectiveGlobalSize / 2f;
        float startY = height / 2f - effectiveGlobalSize / 2f;
        float cellW  = effectiveGlobalSize / 7f;
        float cellH  = effectiveGlobalSize / 7f;

        System.Array.Fill(pixelBuffer, Color.white);
        DrawFixationCross(pixelBuffer);

        for (int ix = 0; ix < width; ix++)
        {
            for (int iy = 0; iy < height; iy++)
            {
                int globalRow = (int)((iy - startY) / cellH);
                int globalCol = (int)((ix - startX) / cellW);

                if (globalRow >= 0 && globalRow < 7 && globalCol >= 0 && globalCol < 7)
                {
                    if (globalPattern[6 - globalRow, globalCol] == 1)
                    {
                        float cellStartX = startX + globalCol * cellW + spacing / 2f;
                        float cellStartY = startY + globalRow * cellH + spacing / 2f;

                        int localRow = (int)((iy - cellStartY) / (localLetterSize / 7f));
                        int localCol = (int)((ix - cellStartX) / (localLetterSize / 7f));

                        if (localRow >= 0 && localRow < 7 && localCol >= 0 && localCol < 7)
                        {
                            if (localPattern[6 - localRow, localCol] == 1)
                                pixelBuffer[ix + iy * width] = Color.black;
                        }
                    }
                }
            }
        }

        Texture2D tex = new Texture2D(width, height);
        tex.SetPixels(pixelBuffer);
        tex.Apply();
        return tex;
    }

    private void GenerateStimulusForE()
    {
        // 50% chance of active vs inactive
        bool showActive = forceTargetPresent ? true : Random.Range(0f, 1f) < 0.5f;
        
        if (showActive)
        {
            // ACTIVE TRIALS (E present - say YES)
            navonP.trialCategory = "Active";
            navonP.targetPresent = true;
            
            int activeType = Random.Range(0, 3);
            
            switch (activeType)
            {
                case 0:  // Big E, Little E (Congruent)
                    navonP.stimulusType = experimentParameters.StimulusType.E_BigE_LittleE;
                    navonP.globalLetter = 'E';
                    navonP.localLetter = 'E';
                    navonP.isCongruent = true;
                    break;
                case 1:  // Big E, Little I (Global-only, incongruent)
                    navonP.stimulusType = experimentParameters.StimulusType.E_BigE_LittleI;
                    navonP.globalLetter = 'E';
                    navonP.localLetter = 'I';
                    navonP.isCongruent = false;
                    break;
                case 2:  // Big I, Little E (Local-only, incongruent)
                    navonP.stimulusType = experimentParameters.StimulusType.E_BigI_LittleE;
                    navonP.globalLetter = 'I';
                    navonP.localLetter = 'E';
                    navonP.isCongruent = false;
                    break;
            }
        }
        else
        {
            // INACTIVE TRIALS (E absent - say NO)
            navonP.trialCategory = "Inactive";
            navonP.targetPresent = false;
            
            int inactiveType = Random.Range(0, 3);
            
            switch (inactiveType)
            {
                case 0:  // Big F, Little F (Congruent foil)
                    navonP.stimulusType = experimentParameters.StimulusType.E_BigF_LittleF;
                    navonP.globalLetter = 'F';
                    navonP.localLetter = 'F';
                    navonP.isCongruent = true;
                    break;
                case 1:  // Big T, Little F (Incongruent foil)
                    navonP.stimulusType = experimentParameters.StimulusType.E_BigT_LittleF;
                    navonP.globalLetter = 'T';
                    navonP.localLetter = 'F';
                    navonP.isCongruent = false;
                    break;
                case 2:  // Big F, Little T (Incongruent foil)
                    navonP.stimulusType = experimentParameters.StimulusType.E_BigF_LittleT;
                    navonP.globalLetter = 'F';
                    navonP.localLetter = 'T';
                    navonP.isCongruent = false;
                    break;
            }
        }
    }

    private void GenerateStimulusForT()
    {
        // 50% chance of active vs inactive
        bool showActive = forceTargetPresent ? true : Random.Range(0f, 1f) < 0.5f;
        
        if (showActive)
        {
            // ACTIVE TRIALS (T present - say YES)
            navonP.trialCategory = "Active";
            navonP.targetPresent = true;
            
            int activeType = Random.Range(0, 3);
            
            switch (activeType)
            {
                case 0:  // Big T, Little T (Congruent)
                    navonP.stimulusType = experimentParameters.StimulusType.T_BigT_LittleT;
                    navonP.globalLetter = 'T';
                    navonP.localLetter = 'T';
                    navonP.isCongruent = true;
                    break;
                case 1:  // Big T, Little E (Global-only, incongruent)
                    navonP.stimulusType = experimentParameters.StimulusType.T_BigT_LittleE;
                    navonP.globalLetter = 'T';
                    navonP.localLetter = 'E';
                    navonP.isCongruent = false;
                    break;
                case 2:  // Big E, Little T (Local-only, incongruent)
                    navonP.stimulusType = experimentParameters.StimulusType.T_BigE_LittleT;
                    navonP.globalLetter = 'E';
                    navonP.localLetter = 'T';
                    navonP.isCongruent = false;
                    break;
            }
        }
        else
        {
            // INACTIVE TRIALS (T absent - say NO)
            navonP.trialCategory = "Inactive";
            navonP.targetPresent = false;
            
            int inactiveType = Random.Range(0, 3);
            
            switch (inactiveType)
            {
                case 0:  // Big F, Little F (Congruent foil)
                    navonP.stimulusType = experimentParameters.StimulusType.T_BigF_LittleF;
                    navonP.globalLetter = 'F';
                    navonP.localLetter = 'F';
                    navonP.isCongruent = true;
                    break;
                case 1:  // Big I, Little F (Incongruent foil)
                    navonP.stimulusType = experimentParameters.StimulusType.T_BigI_LittleF;
                    navonP.globalLetter = 'I';
                    navonP.localLetter = 'F';
                    navonP.isCongruent = false;
                    break;
                case 2:  // Big F, Little I (Incongruent foil)
                    navonP.stimulusType = experimentParameters.StimulusType.T_BigF_LittleI;
                    navonP.globalLetter = 'F';
                    navonP.localLetter = 'I';
                    navonP.isCongruent = false;
                    break;
            }
        }
    }

    private int[,] GetLetterPattern(char letter)
    {
        switch (letter)
        {
            case 'E': return letterE;
            case 'T': return letterT;
            case 'F': return letterF;
            case 'I': return letterI;
            default:
                Debug.LogWarning($"Unknown letter: {letter}, defaulting to E");
                return letterE;
        }
    }

    Texture2D GenerateFixationCross()
    {
        fixationTexture = new Texture2D(width, height);
        System.Array.Fill(pixelBuffer, Color.white);
        DrawFixationCross(pixelBuffer);
        fixationTexture.SetPixels(pixelBuffer);
        fixationTexture.Apply();
        return fixationTexture;
    }

    void DrawFixationCross(Color[] pixels)
    {
        int centerX = width / 2;
        int centerY = height / 2;
        int crossSize = 10;
        int crossThickness = 2;

        // Horizontal line
        for (int x = centerX - crossSize; x <= centerX + crossSize; x++)
        {
            for (int t = 0; t < crossThickness; t++)
            {
                int py = centerY + t - crossThickness / 2;
                if (x >= 0 && x < width && py >= 0 && py < height)
                    pixels[x + py * width] = Color.black;
            }
        }

        // Vertical line
        for (int y = centerY - crossSize; y <= centerY + crossSize; y++)
        {
            for (int t = 0; t < crossThickness; t++)
            {
                int px = centerX + t - crossThickness / 2;
                if (px >= 0 && px < width && y >= 0 && y < height)
                    pixels[px + y * width] = Color.black;
            }
        }
    }

    Texture2D GenerateMask()
    {
        // White background via buffer (same approach as Navon textures).
        System.Array.Fill(pixelBuffer, Color.white);

        // 7x7 grid of hash marks - slightly bigger than the stimulus
        const int gridSize = 7;
        const int hashSize = 55; // slightly bigger than the 50px local letters
        const int spacing  = 12;

        // Total grid: 7 × (55 + 12) - 12 = 457 pixels (covers 400px stimulus)
        int totalGridWidth  = (hashSize + spacing) * gridSize - spacing;
        int totalGridHeight = (hashSize + spacing) * gridSize - spacing;

        int startX = (width  - totalGridWidth)  / 2;
        int startY = (height - totalGridHeight) / 2;

        for (int gridRow = 0; gridRow < gridSize; gridRow++)
        {
            for (int gridCol = 0; gridCol < gridSize; gridCol++)
            {
                int hashStartX = startX + gridCol * (hashSize + spacing);
                int hashStartY = startY + gridRow * (hashSize + spacing);
                DrawHashMark(pixelBuffer, hashStartX, hashStartY, hashSize);
            }
        }

        maskTexture = new Texture2D(width, height);
        maskTexture.SetPixels(pixelBuffer);
        maskTexture.Apply();
        return maskTexture;
    }

    void DrawHashMark(Color[] pixels, int startX, int startY, int size)
    {
        const int lineThickness = 5; // 5 pixels thick for better visibility

        int verticalBar1   = size / 3;
        int verticalBar2   = (size * 2) / 3;
        int horizontalBar1 = size / 3;
        int horizontalBar2 = (size * 2) / 3;

        // Two vertical bars
        for (int y = 0; y < size; y++)
        {
            int py = startY + y;
            if (py < 0 || py >= height) continue;
            for (int t = 0; t < lineThickness; t++)
            {
                int px1 = startX + verticalBar1 + t;
                if (px1 >= 0 && px1 < width) pixels[px1 + py * width] = Color.black;
                int px2 = startX + verticalBar2 + t;
                if (px2 >= 0 && px2 < width) pixels[px2 + py * width] = Color.black;
            }
        }

        // Two horizontal bars
        for (int x = 0; x < size; x++)
        {
            int px = startX + x;
            if (px < 0 || px >= width) continue;
            for (int t = 0; t < lineThickness; t++)
            {
                int py1 = startY + horizontalBar1 + t;
                if (py1 >= 0 && py1 < height) pixels[px + py1 * width] = Color.black;
                int py2 = startY + horizontalBar2 + t;
                if (py2 >= 0 && py2 < height) pixels[px + py2 * width] = Color.black;
            }
        }
    }

    
}
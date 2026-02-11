using UnityEngine;
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.UIElements.Experimental;
using UnityEngine.XR.Interaction.Toolkit.Utilities.Tweenables.Primitives;
using Unity.VisualScripting;
using TMPro;
using JetBrains.Annotations;
using System.Collections;

public class runExperiment : MonoBehaviour
{
    // This is the launch script for the experiment, useful for toggling certain input states. 

    //Navon v1  -UTS 


    [Header("User Input")]
    public bool playinVR;
    public string participant;
    public bool skipWalkCalibration;


    [Header("Experiment State")]

    public string responseMapping = "L:absent R:present"; // show for experimenter (default)
    public int trialCount;
    public float trialTime;
    public float thisTrialDuration;
    public bool trialinProgress;
    [SerializeField] private int responseMap; // for assigning left/right to detect/reject [-1, 1];


    [HideInInspector]
    public int detectIndex, targState, blockType; // 

    
    [HideInInspector]
    public bool isStationary, collectTrialSummary, collectEventSummary, hasResponded;

    // Protected stimulus data
    [HideInInspector]
    public experimentParameters.DetectionTask currentDetectionTask;
    [HideInInspector]
    public experimentParameters.StimulusType currentStimulusType;
    [HideInInspector]
    public bool currentTargetPresent;
    [HideInInspector]
    public char currentGlobalLetter;
    [HideInInspector]
    public char currentLocalLetter;
    [HideInInspector]
    public bool currentIsCongruent;
    [HideInInspector]
    public string currentTrialCategory;
    private bool updateNextNavon;
    

    [HideInInspector]
    public string[] responseforPresentAbsent; // grabbed by showText.
    
    bool SetUpSession;

    //todo
    //public bool forceheightCalibration;
    //public bool forceEyecalibration;
    //public bool recordEEG;
    //public bool isEyetracked;


    CollectPlayerInput playerInput;
    experimentParameters expParams;
    controlWalkingGuide controlWalkingGuide;
    WalkSpeedCalibrator walkCalibrator;
    ShowText ShowText;
    FeedbackText FeedbackText;
    targetAppearance targetAppearance;
    RecordData RecordData;
    // QuestStaircase QuestStaircase;
    AdaptiveStaircaseSlow adaptiveStaircaseSlow;
    AdaptiveStaircaseNatural adaptiveStaircaseNatural;
    
    makeNavonStimulus makeNavonStimulus;

    //use  serialize field to require drag-drop in inspector. less expensive than GameObject.Find() .
    [SerializeField] GameObject TextScreen;
    [SerializeField] GameObject TextFeedback;
    [SerializeField] GameObject StimulusScreen;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {


        adaptiveStaircaseSlow = GetComponent<AdaptiveStaircaseSlow>();
        adaptiveStaircaseNatural = GetComponent<AdaptiveStaircaseNatural>();
        playerInput = GetComponent<CollectPlayerInput>();
        expParams = GetComponent<experimentParameters>();
        controlWalkingGuide = GetComponent<controlWalkingGuide>();
        walkCalibrator = GetComponent<WalkSpeedCalibrator>();
        RecordData = GetComponent<RecordData>();

        ShowText = TextScreen.GetComponent<ShowText>();
        FeedbackText = TextFeedback.GetComponent<FeedbackText>();

        targetAppearance = StimulusScreen.GetComponent<targetAppearance>();
        makeNavonStimulus = StimulusScreen.GetComponent<makeNavonStimulus>();
        // hide player camera if not in VR (useful for debugging).
        togglePlayers();

        // flip coin for responsemapping:
        assignResponses(); // assign Left/RIght clicks to above/below average(random)
        
        trialCount = 0;    
        trialinProgress = false;

        trialTime = 0f;
        collectEventSummary = false; // send info after each target to csv file.
        
        hasResponded = false;
        
        updateNextNavon=false;
        
        SetUpSession = true;

    }

    // Update is called once per frame
    void Update()
    {
        if (SetUpSession && ShowText.isInitialized)
        {
            if (skipWalkCalibration)
            {
                // show welcome 
                ShowText.UpdateText(ShowText.TextType.CalibrationComplete);                
            }
            else
            {
                // show welcome 
                ShowText.UpdateText(ShowText.TextType.Welcome);
            }
            SetUpSession = false;
        }


        //pseudo code: 
        // listen for trial start (input)/
        // if input. 1) start the walking guide movement
        //           2) start the within trial co-routine
        //           3) start the data recording.

        if (!trialinProgress && playerInput.botharePressed)
        {

            // if we have not yet calibrated walk speed, simply move the wlaking guide to start loc:
            if (playinVR)
            {
                if (walkCalibrator.isCalibrationComplete())
                {
                    //start trial sequence, including:
                    // movement, co-routine, datarecording.

                    Debug.Log("Starting Trial in VR mode");
                    startTrial();
                }
                else
                {
                    Debug.Log("button pressed but walk calibration still in progress");
                    // lets hide the walking guide temporarily. 
                    controlWalkingGuide.setGuidetoHidden();
                }
            }
            else // not in VR, skip calibration:
            {
                // Non-VR mode: skip calibration check and start trial directly
                Debug.Log("Starting Trial (Non-VR mode)");
                startTrial();
            }

        }

        // increment trial time.
        if (trialinProgress)
        {
            trialTime += Time.deltaTime; // increment timer.

            if (trialTime > thisTrialDuration)
            {
                trialPackDown(); // includes trial incrementer
                trialCount++;
            }

            if (trialTime < 0.5f || hasResponded)
            {
                return; // do nothing if early, or if already processed a reponse for current event
            }

            if (playerInput.anyarePressed)
            {
                processPlayerResponse(); // determines if a 'Detect' or 'Reject' based on controller mappings.
            }



        }


        // // process no response (TO DO):
        // if (targetAppearance.processNoResponse) // i.e. no reponse was recorded ,this value is set in the targetAppearance coroutine.
        // {
        //     Debug.Log("No Response, and No update to staircase, regenerating...");
        //     //flip if present/absent on next trial:
        //     makeGaborTexture.gaborP.signalPresent = UnityEngine.Random.Range(0f, 1f) < 0.5f ? true : false;   // Changed from 0.66 as we have changed lower asymptote to 0 (pThreshold now 0.5, not 0.75)            
        //     makeGaborTexture.GenerateGabor(makeGaborTexture.gaborP.sAmp); // using the current intensity            
        //     updateNextGabor = false; // perform once only   
        //      targetAppearance.processNoResponse = false;
        // }

    } //end Update()

    
        
        
    

    void togglePlayers()
    {
        if (playinVR)
        {
            GameObject.Find("VR_Player").SetActive(true);
            GameObject.Find("Kb_Player").SetActive(false);
        }
        else
        {
            GameObject.Find("VR_Player").SetActive(false);
            GameObject.Find("Kb_Player").SetActive(true);

        }
    }
    void processPlayerResponse()
    {

        // first place the click into our array for subsequent recording
        expParams.trialD.clickOnsetTime = trialTime;
        

        if (hasResponded || detectIndex <= 0) // if response already processed, eject
        {
            return;
        }

    
        // Use protected variables
        experimentParameters.DetectionTask taskType = currentDetectionTask;
        experimentParameters.StimulusType stimType = currentStimulusType;
        bool targetPresent = currentTargetPresent;
        char globalLetter = currentGlobalLetter;
        char localLetter = currentLocalLetter;
        bool isCongruent = currentIsCongruent;
        string trialCategory = currentTrialCategory;
        
        if (expParams.trialD.targetPresent == true) // signal present cases 
        {
            // HIT or FA based on response mapping:
            if ((responseMap == 1 && playerInput.rightisPressed) || (responseMap == -1 && playerInput.leftisPressed))
            {
                Debug.Log("Hit!");
                // HIT!           
                expParams.trialD.targCorrect = 1;
                expParams.trialD.targResponse = 1;
            }
            else if ((responseMap == 1 && playerInput.leftisPressed) || (responseMap == -1 && playerInput.rightisPressed))
            {
                Debug.Log("Miss!");

                expParams.trialD.targCorrect = 0;
                expParams.trialD.targResponse = 0;
            }

        }
        else if (expParams.trialD.targetPresent == false) // signal absent yet detected
        {

            // HIT or FA based on response mapping:
            if ((responseMap == 1 && playerInput.rightisPressed) || (responseMap == -1 && playerInput.leftisPressed))
            {
                Debug.Log("False Alarm!");
                // HIT!           
                expParams.trialD.targCorrect = 0;
                expParams.trialD.targResponse = 1;
            }
            else if ((responseMap == 1 && playerInput.leftisPressed) || (responseMap == -1 && playerInput.rightisPressed))
            {
                Debug.Log("Correct Rejection!");

                expParams.trialD.targCorrect = 1;
                expParams.trialD.targResponse = 0;
            }

        }
        // store remaining trial data:
        
        expParams.trialD.currentTask = taskType;
        expParams.trialD.stimulusType = stimType;
        expParams.trialD.targetPresent = targetPresent;
        expParams.trialD.globalLetter = globalLetter;
        expParams.trialD.localLetter = localLetter;
        expParams.trialD.isCongruent = isCongruent;
        expParams.trialD.trialCategory = trialCategory;
        


        RecordData.extractEventSummary();// = true;// pass to Record Data (after every hit /FA target)

        hasResponded = true; // passed to coroutine, avoids processing omitted responses.

        // Now update stimulus after each response

        // send the information to AdaptiveStaircase and makeGabor.

        if (trialCount >= expParams.nstandingStilltrials)
        {
            // float nextIntensity = makeGaborTexture.gaborP.sAmp; //default 
            float nextDuration = makeNavonStimulus.navonP.targDuration; //default 

            bool wasCorrect = expParams.trialD.targCorrect == 1 ? true : false; // return true or flse based on targCorrect [1,0].

            if (expParams.trialD.blockType == 1) //slow speed
            {
             nextDuration = adaptiveStaircaseSlow.ProcessResponse(wasCorrect); 
            } // return new intensity
            else if (expParams.trialD.blockType == 2) //natural speed
            {
                nextDuration = adaptiveStaircaseNatural.ProcessResponse(wasCorrect); // return new intensity
            }


            Debug.Log($"[Staircase] Stimulus processed: {(wasCorrect ? "✓" : "✗")} → Next duration: {nextDuration:F3}s");

            //apply new intensity (targetPresent is randomized inside GenerateNavon)
            makeNavonStimulus.navonP.targDuration= nextDuration;
            makeNavonStimulus.GenerateNavon(); // creates new texture

            //note that this creates the texture , but not applied until ShowNavon();
            Debug.Log("correct: " + expParams.trialD.targCorrect + ", new quest value for staircase [" + blockType + "] = " + nextDuration);

        }
        else
        {
            // just regenerate without updating contrast, provide feedback also (targetPresent is randomized inside GenerateNavon).
            Debug.Log("Still in practice trials, regenerating... ");
            makeNavonStimulus.GenerateNavon(); // regenerate with current duration
            
            if (expParams.trialD.targCorrect == 1)
            {
                FeedbackText.UpdateText(FeedbackText.TextType.Correct);
                //using Unity's Invoke, hide after small duration.
                Invoke(nameof(HideFeedbackText), 0.2f); // inovke requires name of method as a string.

            }
            else
            {
                FeedbackText.UpdateText(FeedbackText.TextType.Incorrect);
                Invoke(nameof(HideFeedbackText), 0.2f); // inovke requires name of method as a string.
            }
            

        }
                
    }
    private void HideFeedbackText()
    {
        FeedbackText.UpdateText(FeedbackText.TextType.Hide);
    }

    void startTrial()
    {
        // This method handles the trial sequence.
        //// First ensure some parameters are set, then launch the coroutine and

        //recalibrate screen height to participants HMD
        controlWalkingGuide.updateScreenHeight();
        //remove text
        ShowText.UpdateText(ShowText.TextType.Hide);
        FeedbackText.UpdateText(FeedbackText.TextType.Hide);

        //establish trial parameters:
        // Calculate max targets if not already done (should happen after calibration)
        if (expParams.maxTargsbySpeed == null)
        {
            // expParams.CalculateMaxTargetsBySpeed();
        }

        trialinProgress = true; // for coroutine (handled in targetAppearance.cs).        
        ShowText.UpdateText(ShowText.TextType.Hide);
        trialTime = 0;
        targState = 0; //target is hidden. 

        //Establish (this trial) specific parameters:
        blockType = expParams.blockTypeArray[trialCount, 2]; //third column [0,1,2].

        // thisTrialDuration = expParams.GetTrialDuration(); //thisTrialDuration passed to targetAppearance.cs
        thisTrialDuration = expParams.walkDuration; // all trials the same duration now, distance varies instead.

        //query if stationary (restricts movement guide)
        isStationary = blockType == 0 ? true : false;

        //populate public trialD structure for extraction in recordData.cs

        // add to public struct trialD for recordData.cs and other scripts
        expParams.trialD.trialNumber = trialCount;
        expParams.trialD.blockID = expParams.blockTypeArray[trialCount, 0];
        expParams.trialD.trialID = expParams.blockTypeArray[trialCount, 1]; // count within block
        expParams.trialD.isStationary = isStationary;
        expParams.trialD.blockType = blockType; // 0,1,2

        // Set detection task from balanced block assignment
        int currentBlockID = expParams.blockTypeArray[trialCount, 0];
        var newTask = expParams.blockDetectionTask[currentBlockID];

        // Regenerate stimulus if the task changed (new block with different target letter)
        if (makeNavonStimulus.navonP.currentTask != newTask)
        {
            makeNavonStimulus.navonP.currentTask = newTask;
            makeNavonStimulus.GenerateNavon();
        }
        else
        {
            makeNavonStimulus.navonP.currentTask = newTask;
        }

        //updated phases for flow managers:
        RecordData.recordPhase = RecordData.phase.collectResponse;

        // if not a stationary trial, start movement guide.
        if (!isStationary)
        {
            controlWalkingGuide.moveGuideatWalkSpeed();
        }

        //start coroutine to control target onset and target behaviour:
        print("Starting Trial " + (trialCount + 1) + " of " + expParams.nTrialsperBlock);

        targetAppearance.startSequence(); // co routine in another script.

    }

    void trialPackDown()
    {
        // This method handles the end of a trial, including data recording and cleanup.
        Debug.Log("End of Trial " + (trialCount + 1));

        // For safety
        RecordData.recordPhase = RecordData.phase.stop;
        //determine next start position for walking guide.

        controlWalkingGuide.SetGuideForNextTrial(); //uses current trialcount +1 to determine next position.

        // Reset trial state
        trialinProgress = false;
        trialTime = 0f;

        // Hide target appearance
        makeNavonStimulus.hideNavon();


        // Update text screen to show next steps or end of experiment
        ShowText.UpdateText(ShowText.TextType.TrialStart); //using the previous trial count to show next trial info.


    }

    void assignResponses()
    {
        bool switchmapping = UnityEngine.Random.Range(0f, 1f) < 0.5f ? true : false;

        ////Hack
        //// To force L:Present R:absent
        //bool switchmapping = true;
        //// To force L:absent R:Present
        //bool switchmapping = false;


        responseforPresentAbsent = new string[2];

        if (switchmapping)
        {
            responseMap = -1;
            responseMapping = "L:Present R:absent";
            responseforPresentAbsent[0] = "Left click"; //present
            responseforPresentAbsent[1] = "Right click"; //absent
        }
        else
        {
            responseMap = 1;
            responseforPresentAbsent[0] = "Right click"; //present
            responseforPresentAbsent[1] = "Left click"; //absent
            responseMapping = "L:Absent R:Present";

        }
    }



}

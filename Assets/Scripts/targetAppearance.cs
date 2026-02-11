using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Composites;

public class targetAppearance : MonoBehaviour
{
    /// <summary>
    /// Handles the co-routine to precisely time changes to target appearance during walk trajectory.
    /// 
    /// Main method called from runExperiment.


    public bool processNoResponse;
    private float waitTime;
    private float trialDuration;
    private float[]  trialOnsets;
    
    runExperiment runExperiment;
    Renderer rend;
    
    makeNavonStimulus makeNavonStimulus;
    experimentParameters expParams;
    CalculateStimTimes calcStimTimes;
    //Staircase ppantStaircase;

    [SerializeField]
    GameObject scriptHolder;


    private Color targColor;

    bool includeBackwardMask = false;

    private void Start()
    {
        runExperiment = scriptHolder.GetComponent<runExperiment>();
        expParams = scriptHolder.GetComponent<experimentParameters>();
        calcStimTimes = scriptHolder.GetComponent<CalculateStimTimes>();

        // methods:
        makeNavonStimulus = GetComponent<makeNavonStimulus>();
        processNoResponse = false;
        targColor = new Color(1f, 1f, 1f); // rend.material.color; // start target a

        // includeBackwardMask = true;



    }

    public void startSequence()
    {

        // some params for this trial:

        //// note that trial duration changes with walk speed.
        trialDuration = runExperiment.thisTrialDuration;

        
        makeNavonStimulus.hideNavon();

        // note that onsets are now precalclated:
        
        trialOnsets = calcStimTimes.allOnsets[runExperiment.trialCount];

        
        // Debug.Log("preparing to show " + maxTargetsThisTrial + "between " + targRange[0] + " and " + targRange[1] + " sec");

        StartCoroutine("trialProgress");
    }

    /// <summary>
    /// Coroutine controlling target appearance with precise timing.
    /// </summary>
    /// 

    IEnumerator trialProgress()
    {
        while (runExperiment.trialinProgress) // this creates a never-ending loop for the co-routine.
        {
            // trial progress:
            // / The timing of trial elements is determined on the fly.
            // / Boundaries set in trialParameters.
            // begin target presentation:
            runExperiment.detectIndex = 0; // listener, to assign correct responses per target [0 = FA, 1 = targ1, 2 = targ 2]

            yield return new WaitForSecondsRealtime(expParams.preTrialsec);


            // show target [use duration or colour based on staircase method].
            //// however many targets we have to present this trial, cycle through and present

            for (int itargindx = 0; itargindx < trialOnsets.Length; itargindx++)
            {
                // first target has no ISI adjustment
                if (itargindx == 0)
                {
                    waitTime = trialOnsets[0];
                }
                else
                {// adjust for time elapsed.
                    waitTime = trialOnsets[itargindx] - runExperiment.trialTime;
                }

                // wait before presenting target:
                yield return new WaitForSecondsRealtime(waitTime);



                // to increase difficulty, and remove expectancy, only show on the % of trials.
                if (Random.value <= .95f) // proportion to show targets (now have jitter also).
                {


                    //setColour(trialParams.targetColor);
                    makeNavonStimulus.showNavon();
                    runExperiment.targState = 1; // target is shown
                    runExperiment.detectIndex = itargindx + 1; //  click responses collected in this response window will be 'correct'
                    runExperiment.hasResponded = false;  //switched if targ detected.
                    
                    // Freeze all stimulus properties into an immutable event at this
                    // exact moment. Once created, this snapshot cannot be changed —
                    // so even when GenerateNavon() later overwrites navonP for the
                    // next stimulus, the response handler and data recorder will still
                    // see the correct values for *this* presentation.
                    runExperiment.currentEvent = new experimentParameters.StimulusEvent(
                        detectionTask: makeNavonStimulus.navonP.currentTask,
                        stimulusType:  makeNavonStimulus.navonP.stimulusType,
                        globalLetter:  makeNavonStimulus.navonP.globalLetter,
                        localLetter:   makeNavonStimulus.navonP.localLetter,
                        targetPresent: makeNavonStimulus.navonP.targetPresent,
                        isCongruent:   makeNavonStimulus.navonP.isCongruent,
                        trialCategory: makeNavonStimulus.navonP.trialCategory,
                        onsetTime:     runExperiment.trialTime
                    );

                    
                    // Use adaptive stimulus duration
                    float currentStimulusDuration =  makeNavonStimulus.navonP.targDuration; // function call?
                    yield return new WaitForSecondsRealtime(currentStimulusDuration);
                    // BACKWARD MASK: Show mask AFTER stimulus for 30ms
                    
                    if (includeBackwardMask)
                    {
                        makeNavonStimulus.backwardMask();  // Shows the hash grid
                        yield return new WaitForSecondsRealtime(0.03f);  // 30ms mask     
                        makeNavonStimulus.hideNavon();
                        runExperiment.targState = 0; // target has been removed
                        //adjust resposne window
                        yield return new WaitForSecondsRealtime(expParams.responseWindow - .03f);
                    } else
                    {
                       makeNavonStimulus.hideNavon();
                       runExperiment.targState = 0; // target has been removed
                        yield return new WaitForSecondsRealtime(expParams.responseWindow);
                    }
                    
                    // if no click in time, count as a miss.
                    if (!runExperiment.hasResponded) // no response 
                    {
                        processNoResponse = true; // handled in runExperiment.
                    }
                    runExperiment.detectIndex = 0; //clicks from now could be counted as incorrect (too slow).  //runExperiment.targCount++;
                }
                else // hide target
                {
                    Debug.Log("Hiding target");
                    // no colour change, no change to targ state, detectindex=0,
                    //how long to show target for?
                    yield return new WaitForSecondsRealtime(expParams.targDurationsec);
                    yield return new WaitForSecondsRealtime(expParams.responseWindow);
                    //trialParams.trialD.targOnsetTime = 0;
                    processNoResponse = false; // don't count as a miss (since no targets).
                }
            }// for each target

            // after for loop, wait for trial end:
            while (runExperiment.trialTime < runExperiment.thisTrialDuration)
            {
                yield return null;  // wait until next frame. 
            }

            break; //Trial Complete, exit the while loop.

        } // while trial in progress

    } // IEnumerator


}

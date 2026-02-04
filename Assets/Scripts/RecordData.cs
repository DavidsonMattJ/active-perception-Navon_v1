using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Xml.Linq;
using UnityEditor;

public class RecordData : MonoBehaviour
{
    /// <summary>
    /// This script produces a simple output to test write/save features.
    /// upon Left click, the data stream is recorded (head pos - xyz pozition).
    /// after 1 sec duration, it is then written to disk at location *outputFolder*
    /// </summary>


    // updated 2025-05 MD to enable gaze origin and direction recordings.
    // preallocate output file, and folder
    public string outputFile_pos, outputFile_posEye, outputFile_summary, outputFolder;
    List<string> outputData_pos = new List<string>();
    List<string> outputData_summary = new List<string>();
    public string startTime;
    float data_trialTime;
    //assign public GameObj for easey access to hmd and gaze daa (controllers too if needed.)
    public GameObject objHMD; // drag and drop
    public GameObject objGazeInteractor;

    //talk to:
    CollectPlayerInput CollectPlayerInput;
    GazeandEyeMethods GazeandEyeMethods;
    runExperiment runExperiment;

    experimentParameters experimentParameters;
    private bool clickStateL, clickStateR; // to test if input is saved from triggers

    //access the gaze data
    float trialTime;
    //flow handler:
    private bool dataSaveinprogres;
    string gazeObject;
    string projectName = "VIS2AFC_v2";


    public enum phase // these fields can be set by other scripts (runExperiment) to control record state.
    {
        idle,
        collectResponse,
        collectTrialSummary,
        stop
    };

    //set to idle to begin with:
    public phase recordPhase = phase.idle;

    // Start is called before the first frame update
    void Start()
    {

        //make sure we have access to all components. 
        CollectPlayerInput = GetComponent<CollectPlayerInput>(); // on same GameObj.
        runExperiment = GetComponent<runExperiment>();
        experimentParameters = GetComponent<experimentParameters>();
        GazeandEyeMethods = GetComponent<GazeandEyeMethods>();

        //set up output details:
        outputFolder = GetOutputFolder();
        Debug.Log("saving to location " + outputFolder);

        startTime = System.DateTime.Now.ToString("yyyy-MM-dd-hh-mm");
        dataSaveinprogres = false; // 

        // only create if playing in VR. 
        if (runExperiment.playinVR)
        {
            createPositionTextfile();

        }
        createSummaryTextfile(); // each row is populated based on single events (targets shown or not).



    }

    // Update is called once per frame
    void Update()
    {

        if (recordPhase == phase.idle)
        {
            if (data_trialTime > 0)
            {
                data_trialTime = 0; //reset
            }
        }

        if (recordPhase == phase.collectResponse)
        {
            // write each frame to our file:
            if (runExperiment.playinVR)
            {
                writePositionData(); // also increments trialTime for the datasave.
            }
        }

        if (recordPhase == phase.stop) //
        {
            //write to disk.
        
            writeFiletoDisk();
            
            dataSaveinprogres = false;
        }

    }



    public void createPositionTextfile()
    {

        // blockID
        // is updated by the runExp.trialPackdown method, only on the last trial of each block (after data for that trial has been saved).

        outputFile_pos = outputFolder + runExperiment.participant  + "_" + startTime + ".csv";


        string columnNamesPos = "trialTime," +
            "clickstate_L," +
            "clickstate_R," +
            "head_X," +
            "head_Y," +
            "head_Z," +
            "gazeOrigin_X," +
            "gazeOrigin_Y," +
            "gazeOrigin_Z," +
            "gazeDirection_X," +
            "gazeDirection_Y," +
            "gazeDirection_Z," +
            "gazeHit_X," +
            "gazeHit_Y," +
            "gazeHit_Z," +
            "gazeHitObject," +
            "gazeAngularSpeed" +
            "\r\n";

        File.WriteAllText(outputFile_pos, columnNamesPos);

    }


    public void writePositionData()
    {

        Vector3 currentHead = objHMD.transform.position;
        clickStateL = CollectPlayerInput.leftisPressed;
        clickStateR = CollectPlayerInput.rightisPressed;

        // NB that the position in the Transform of the GazeInteration object is the gaze origin. 
        //This is where the gaze ray starts from, calculated as the centre point between the eyes in the virtual space.
        //The Rotation (and forward vector derived from this rotation) represents the gaze direction/which way you are looking.

        Vector3 gazeDirection = objGazeInteractor.transform.forward;
        Vector3 gazeOrigin = objGazeInteractor.transform.position;
        GameObject gazeHitObject = GazeandEyeMethods.GazeHitObject;
        float gazeAngularSpeed = GazeandEyeMethods.GazeAngularSpeed;
        
        // test if gazeHitObject is the "Screen" object, 
        if (gazeHitObject != null)
        {
            gazeObject = gazeHitObject.name;
            //shorten the string to make things easier:
            // if contains Screen (UnityEngine.GameObject)), =1, 0 otherwise
            if (gazeObject.Contains("Screen"))
            {
                gazeObject = "Screen";
            }   else
            {
                //
                gazeObject = "notScreen";
            }
        }
        else
        {
            gazeObject = "null";
        }

        // gazeHitObject='test';
        //attempt to get the hitpoint in world space of the ray cast from eyes: 
        //Currently NOT WORKING: TO DO AND UPDATE.
        
        Vector3 gazeHit = GazeandEyeMethods.GazeHitPosition;
        // Vector3 gazeHit = gazeDirection;
        // float pupilDiameter = GazeandEyeMethods.AveragePupilDiameter;
        // float pupilDiameter = 0f; // UPDATE

        // DATA input must match the order in create_positionTextFile() above:
        // left, right, xyz.
        // per frame, append the relevant data to per column of our datastructure:
        string data =
                runExperiment.trialTime + "," +
                clickStateL + "," +//    "clickstateL," +
                clickStateR + "," +//"clickstateR," +
                currentHead.x + "," + //"headX," +
                currentHead.y + "," + //"headY," +
                currentHead.z + "," + //"headZ," +
                gazeOrigin.x + "," + //"gazeOX," +
                gazeOrigin.y + "," + //             
                gazeOrigin.z + "," + //             
                gazeDirection.x + "," + //gazeDirection (forward)
                gazeDirection.y + "," + //
                gazeDirection.z + "," + //
                gazeHit.x + "," + // hit in world space
                gazeHit.y + "," + //
                gazeHit.z + "," + //
                gazeHitObject + "," + //
                gazeAngularSpeed; // +
                // pupilDiameter; // average of left and right.


        outputData_pos.Add(data);


    }
    private void createSummaryTextfile()
    {
        // outputFile_summary = outputFolder + "test" + "_" + System.DateTime.Now.ToString("yyyy-MM-dd-hh-mm") + "_trialsummary.csv";
        outputFile_summary = outputFolder +  runExperiment.participant + "_" + startTime + "_trialsummary.csv";



        // include toneAmp
        string columnNamesSumm = "date," +
           // add experiment: walkingTracking2D
           "participant," +
           "respmap," +
           "trial," +
           "block," +
           "trialID," +
           "walkSpeed," +           
           "targOnset," +
           "targRT," +
           "targDuration," +
           "signalPresent," +
           "targResponse," +
           "correctResponse";  


        //"walkSpeed," +
        //"qStep," +



        //columnNamesSumm += "FA_rt," + "," + "\r\n";

        columnNamesSumm += "\r\n";


        File.WriteAllText(outputFile_summary, columnNamesSumm);


    }


    // use a method to perform on relevant frame at trial end.
    public void extractEventSummary()
    {

        // at the end of each trial (walk trajectory), export the details as a summary.
        // col names specified below (createSummaryTextfile)

        // convert data of interest:

        //float[] FA_rts = runExperiment.FA_withintrial.ToArray();
        //string strfts = "";
        //if (FA_rts.Length > 0)
        //{
        //    // convert float array to string:
        //    for (var i = 0; i < FA_rts.Length; i++)
        //    {
        //        strfts = strfts + FA_rts[i].ToString() + ","; // separates into columns.
        //    }
        //}



        // FILL DATA:
        //    "date," +
        //   "participant," +
        //    "respmap," +
        //    "trial," +
        //    "block," +
        //    "trialID," +
        //    "walkSpeed," +           
        //    "targOnset," +
        //    "targRT," +
        //    "intensity," +
        //    "signalPresent," +
        //    "targResponse," +
        //    "correctResponse";  


        string data =
                  System.DateTime.Now.ToString("yyyy-MM-dd") + "," +
                  runExperiment.participant + "," +
                  runExperiment.responseMapping + "," +
                  runExperiment.trialCount + "," +
                  experimentParameters.trialD.blockID + "," +
                  experimentParameters.trialD.trialID + "," +
                  experimentParameters.trialD.blockType + "," +
                  experimentParameters.trialD.targOnsetTime + "," +
                  experimentParameters.trialD.clickOnsetTime + "," +
                  experimentParameters.trialD.targDuration + "," +
                  experimentParameters.trialD.targPresent + "," +
                  experimentParameters.trialD.targResponse + "," + // coded as present absent [1,0].
                  experimentParameters.trialD.targCorrect;
        //trialParameters.trialD.walkSpeed + "," +
        //trialParameters.trialD.qStep;



        // REAL TIME EYE DIRECTION CHECKS

        //if (runExperiment.isEyeTracked)
        //{
        //    data +=
        //    trialParameters.trialD.intPracticalE + "," + // 
        //    trialParameters.trialD.degPracticalE + ",";

        //    //print(trialParameters.trialD.degPracticalE);
        //}





        // No Longer adding False Alarms
        //data += strfts;

        outputData_summary.Add(data);

        // reset listener:
        runExperiment.collectEventSummary = false; //change to enum in future

    }

    public void saveonBlockEnd()
    {
        saveRecordedDataList(outputFile_pos, outputData_pos);
        saveRecordedDataList(outputFile_summary, outputData_summary);

        // clear cache
        outputData_pos = new List<string>();
        outputData_summary = new List<string>();


        // if (runExperiment.isEyeTracked) // now saving eye tracking data as a separate thing.
        // {
        //     saveRecordedDataList(outputFile_posEye, outputData_posEye);
        //     outputData_posEye = new List<string>();
        // }
    }

    public void writeFiletoDisk()
    {
        if (runExperiment.playinVR)
        { // both
            saveRecordedDataList(outputFile_pos, outputData_pos);
            saveRecordedDataList(outputFile_summary, outputData_summary);
        }
        else
        {
            saveRecordedDataList(outputFile_summary, outputData_summary);
        }

        // clear cache
        outputData_pos = new List<string>();
        outputData_summary = new List<string>();



    }

    private void OnApplicationQuit() // for safety.
    {
        if (runExperiment.playinVR)
        { // both
            saveRecordedDataList(outputFile_pos, outputData_pos);
            saveRecordedDataList(outputFile_summary, outputData_summary);
        }
        else
        {
            saveRecordedDataList(outputFile_summary, outputData_summary);
        }
    }

    static void saveRecordedDataList(string filePath, List<string> dataList)
    {
        // Robert Tobin Keys:
        // I wrote this with System.IO ----- this is super efficient

        using (StreamWriter writeText = File.AppendText(filePath))
        {
            foreach (var item in dataList)
                writeText.WriteLine(item);
        }
    }

    
    



    private string GetOutputFolder()
    {
        string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
        //  projectName defined above
        string parentDir = System.IO.Path.GetDirectoryName(projectRoot);
        string baseOutputPath;

#if UNITY_EDITOR_WIN
            // Windows: C:/Users/[username]/Documents/Unity Projects/UnityOutputData/
            string userProfile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            baseOutputPath = System.IO.Path.Combine(userProfile, "Documents", "Unity Projects", "UnityOutputData");
#elif UNITY_EDITOR_OSX
        // Mac: /Users/[username]/Documents/Unity Projects/UnityOutputData/
        string userHome = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
        baseOutputPath = System.IO.Path.Combine(userHome, "Documents", "Unity Projects", "UnityOutputData");
#else
            // Linux or other: ~/Documents/Unity Projects/UnityOutputData/
            string userHome = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            baseOutputPath = System.IO.Path.Combine(userHome, "Documents", "Unity Projects", "UnityOutputData");
#endif

        return System.IO.Path.Combine(baseOutputPath, projectName) + System.IO.Path.DirectorySeparatorChar;


    }

}

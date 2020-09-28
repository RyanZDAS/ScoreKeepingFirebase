using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Database;

/// <summary>
/// A class that updates the top ten scores on the UI and the Google Firebase database.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager instance;

    [Header("FirebaseInit Reference:")]
    [SerializeField] private FirebaseInit firebaseInit;

    [Header("Score Manager Settings:")]
    [SerializeField] private int maxScoresQuantity = 10;
    [SerializeField] private bool useTestFeatures = false;

    [Header("Score Saving Test:")]
    [SerializeField] private InputField userNameInput;
    [SerializeField] private InputField scoreInput;
    [SerializeField] private Button submitScore;
    [SerializeField] private Text scoreListText;
    [SerializeField] private List<PlayerScoreData> playerScoreDataList = new List<PlayerScoreData>();

    [Header("Debug Settings:")]
    [SerializeField] private bool debugComponent = false;

    // Firebase Stuff
    private FirebaseDatabase _database;
    private const string SCORELISTKEY = "SCORE_LIST"; // DO NOT CHANGE THIS!!!!!
    private DatabaseReference _ref;

    /// <summary>
    /// A struct to contain each piece of score data that is relavent to the current user.
    /// </summary>
    [Serializable]
    public struct PlayerScoreData
    {
        public string userName;
        public int score;
    }

    /// <summary>
    /// A class that is responsible for storing the list of player scores and converting to a JSON file to upload to Google Firebase database.
    /// </summary>
    [Serializable]
    public class ScoreData
    {
        public List<PlayerScoreData> playerScoreDataList;
    }

    /// <summary>
    /// Awake is called before start.
    /// </summary>
    void Awake()
    {
        instance = this;
    }

    /// <summary>
    /// Start is called before the first frame update
    /// </summary>
    void Start()
    {
        StartCoroutine(GetDatabaseReferenceCoroutine());
    }

    /// <summary>
    /// OnDestroy is called when this gameobject is destroyed.
    /// </summary>
    void OnDestroy()
    {
        _ref.ValueChanged -= HandleValueChanged;
        _ref = null;
        _database = null;
    }

    /// <summary>
    /// Update is called once per frame
    /// </summary>
    void Update()
    {
        
    }

    /// <summary>
    /// A coroutine to get the reference for the database once Firebase is initialized.
    /// </summary>
    /// <returns></returns>
    private IEnumerator GetDatabaseReferenceCoroutine()
    {
        if (firebaseInit)
        {
            // Wait until firebaseInit.isFirebaseInitialized is true.
            yield return new WaitUntil(() => firebaseInit.isFirebaseInitialized);

            // Get database reference and subscribe value changes as an event
            _database = FirebaseDatabase.DefaultInstance;
            _ref = _database.GetReference(path: SCORELISTKEY);
            _ref.ValueChanged += HandleValueChanged;
        }
        else
            Debug.LogError("Could not find FirebaseInit object in the scene. Please add FirebaseInit to the scene to get database reference.");
    }

    /// <summary>
    /// A method that adds a new PlayerScoreData to the playerScoreDataList by running the coroutine.
    /// The coroutine handles the execution.
    /// </summary>
    public void AddNewScore()
    {
        if (!isAddNewScoreCoroutineRunning)
            StartCoroutine(AddNewScoreCoroutine());
    }

    /// <summary>
    /// A method that adds a new PlayerScoreData to the playerScoreDataList by running the coroutine.
    /// The coroutine handles the execution.
    /// </summary>
    public void AddNewScore(string name = "", int score = -1)
    {
        if (!isAddNewScoreCoroutineRunning)
            StartCoroutine(AddNewScoreCoroutine(name, score));
    }

    private bool isAddNewScoreCoroutineRunning = false;

    /// <summary>
    /// A coroutine that adds a new PlayerScoreData to the playerScoreDataList by running the coroutine.
    /// </summary>
    /// <returns></returns>
    private IEnumerator AddNewScoreCoroutine(string name = "", int score = -1)
    {
        isAddNewScoreCoroutineRunning = true;

        // Retrieve user name and score value
        string newName = name;
        int newScore = score;

        // If using testing features, use input fields to gain values from test scene. Otherwise, don't.
        if (useTestFeatures)
        {
            // Retrieve user name
            newName = userNameInput.text;

            if (debugComponent)
                Debug.Log("newName: " + newName);

            // Convert score text to int
            newScore = int.Parse(scoreInput.text);

            if (debugComponent)
                Debug.Log("newScore: " + newScore);
        }
        else
        {
            if (string.IsNullOrEmpty(newName) || newName == "")
            {
                Debug.LogError("The newName is empty, null, or has no characters. Only strings with at least one character are allowed for names.");
                yield break;
            }

            if (newScore < 0)
            {
                Debug.LogError("The newScore added contains values less than 0. Only positive integers are allowed for score values.");
                yield break;
            }
        }

        // Make new PlayerScoreData object to add to the list
        PlayerScoreData playerScoreData = new PlayerScoreData();
        playerScoreData.userName = newName;
        playerScoreData.score = newScore;

        if (debugComponent)
        {
            Debug.Log("playerScoreData.userName: " + playerScoreData.userName);
            Debug.Log("playerScoreData.score: " + playerScoreData.score);
        }

        // Be warry of data races at this point if another user is doing the same thing simultanously...
        // Store new value in list
        playerScoreDataList.Add(playerScoreData);

        // Sort greatest to least
        playerScoreDataList.Sort((a, b) => b.score.CompareTo(a.score));

        // Remove least value if list count is over maxScoresQuantity.
        if (playerScoreDataList.Count > maxScoresQuantity)
        {
            playerScoreDataList.Remove(playerScoreDataList[playerScoreDataList.Count - 1]);
        }

        // Make new ScoreData object
        ScoreData newScoreData = new ScoreData();
        newScoreData.playerScoreDataList = playerScoreDataList;

        // Convert list to JSON
        string json = JsonUtility.ToJson(newScoreData);

        if (debugComponent)
            Debug.Log("json: " + Environment.NewLine + json);

        // Upload JSON to Database
        if (_database != null)
        {
            var sendData = _database.GetReference(path: SCORELISTKEY).SetRawJsonValueAsync(json);
            yield return new WaitUntil(() => sendData.IsCompleted);
        }
        else
            Debug.LogError("Firebase database instance not found. Please initialize the database in order to upload data.");

        isAddNewScoreCoroutineRunning = false;
    }

    /// <summary>
    /// A method to retrieve data from Google Firebase database and update the playerScoreDataList. 
    /// The coroutine handles the execution.
    /// </summary>
    public void RetrieveUpToDateScores()
    {
        if (!isRetrieveUpToDateScoresCoroutineRunning)
            StartCoroutine(RetrieveUpToDateScoresCoroutine());
    }

    private bool isRetrieveUpToDateScoresCoroutineRunning = false;

    /// <summary>
    /// A coroutine to retrieve data from Google Firebase database and update the playerScoreDataList. 
    /// </summary>
    /// <param name="e">The ValueChangedEventArgs that is a parameter in HandleValueChanged method.</param>
    /// <returns></returns>
    private IEnumerator RetrieveUpToDateScoresCoroutine(ValueChangedEventArgs e = null)
    {
        isRetrieveUpToDateScoresCoroutineRunning = true;

        // Retrieve current score list in json format.
        string json = string.Empty;

        if (e != null)
        {
            json = e.Snapshot.GetRawJsonValue();

            if (debugComponent)
                Debug.Log("json: " + Environment.NewLine + json);
        }

        // Create some time to retrieve the JSON data.
        yield return new WaitForEndOfFrame();

        if (!string.IsNullOrEmpty(json) && json != "")
        {
            // Make new ScoreData object and Set current score list to playerScoreDataList
            ScoreData newScoreData = JsonUtility.FromJson<ScoreData>(json);
            //playerScoreDataList = JsonUtility.FromJson<List<PlayerScoreData>>(json);
            playerScoreDataList = newScoreData.playerScoreDataList;
        }
        else
            Debug.LogError("RetrievedData is null or empty. Ignore this if current playerScoreDataList has not been sent to Firebase database yet.");

        // Clear list text before adding more strings to it.
        scoreListText.text = "";

        // Update UI
        for (int i = 0; i < playerScoreDataList.Count; i++)
        {
            scoreListText.text += ((i + 1) + ". " + playerScoreDataList[i].userName + " - " + playerScoreDataList[i].score);
            scoreListText.text += Environment.NewLine;
        }

        isRetrieveUpToDateScoresCoroutineRunning = false;
    }

    /// <summary>
    /// A method that will be called every the Google Firebase database reference detects a change.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e">The ValueChangedEventArgs that is used to retrieve the current data from Google Firebase database.</param>
    private void HandleValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (!isRetrieveUpToDateScoresCoroutineRunning)
            StartCoroutine(RetrieveUpToDateScoresCoroutine(e));
    }

    /// <summary>
    /// A method that will clear the playerScoreDataList as well as the Google Firebase database.
    /// The coroutine handles the execution.
    /// </summary>
    public void ClearList()
    {
        if (!isClearListCoroutineRunning)
            StartCoroutine(ClearListCoroutine());
    }

    private bool isClearListCoroutineRunning = false;

    /// <summary>
    /// A method that will clear the playerScoreDataList as well as the Google Firebase database.
    /// </summary>
    /// <returns></returns>
    private IEnumerator ClearListCoroutine()
    {
        isClearListCoroutineRunning = true;

        // Clear the playerScoreDataList and the scoreListText.
        playerScoreDataList.Clear();
        scoreListText.text = "";

        // Make new ScoreData object
        ScoreData newScoreData = new ScoreData();
        newScoreData.playerScoreDataList = playerScoreDataList;

        // Convert list to JSON
        string json = JsonUtility.ToJson(newScoreData);

        if (debugComponent)
            Debug.Log("json: " + Environment.NewLine + json);

        // Upload JSON to Database
        if (_database != null)
        {
            var sendData = _database.GetReference(path: SCORELISTKEY).SetRawJsonValueAsync(json);
            yield return new WaitUntil(() => sendData.IsCompleted);
        }
        else
            Debug.LogError("Firebase database instance not found. Please initialize the database in order to upload data.");

        isClearListCoroutineRunning = false;
    }
}

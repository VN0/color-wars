using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UtilityExtensions;

public struct SubclipInfo {
    public string clipText;
    public float timeAdjustment;
    public SubclipInfo(string clipText="", float timeAdjustment=0) {
        this.clipText = clipText;
        this.timeAdjustment = timeAdjustment;
    }
}

public struct LiveClipInfo {
    public string clipName;
    public List<SubclipInfo> subclipInfo;
    public float preDelay;
    public float postDelay;
    public LiveClipInfo(string clipName, List<SubclipInfo> subclipInfo,
                        float preDelay = 0, float postDelay = 0) {
        this.clipName = clipName;
        this.subclipInfo = subclipInfo;
        this.preDelay = preDelay;
        this.postDelay = postDelay;
    }
}

public class TutorialLiveClips : MonoBehaviour {
    public static TutorialLiveClips instance;
    public static bool runningLiveClips = false;
    // Live Slide Format: (slideObjectName, [subsection_1_text, subsection_2_text...])
    //
    // slideObjectName is the name of a game object in the scene which contains
    // whatever game objects are relevant to this live slide. Probably a few
    // puppet players, maybe a ball.
    //
    // The subsection text is the tutorial text to be displayed by each
    // subsection of a given live slide, in order.
    List<LiveClipInfo> liveClips = new List<LiveClipInfo>() {
        {"1-shoot-pass-and-score",
         new List<SubclipInfo>() {
                {"TOUCH the ball to pick it up", 0.3f},
                {"SHOOT the ball with (A)", 0.3f},
                {"but you must pass to your teammate...", 0.5f},
                "...before you can score a goal"
         }},
        {"2-cant-pass-in-null-zone",
         new List<SubclipInfo>() {
                "passes don't count in the null zone",
         }},
        {"3-stealing-and-blocking",
         new List<SubclipInfo>() {
                {"DASH with (A) at the ball to STEAL", 0.3f},
                "BLOCK steals with your body"
         }},
        {"4-walls",
         new List<SubclipInfo>() {
                "Hold (B) to lay WALLS",
                "Use WALLS to BLOCK the ball",
                "BREAK walls by DASHING"
         }}

    };

    Canvas tutorialCanvas;
    Text infoText;
    Text readyText;
    Dictionary<GameObject, bool> checkin = new Dictionary<GameObject, bool>();
    bool nextSlideForceCheat = false;

    LiveClipInfo currentClip;
    string currentClipName;
    int currentSubclipIndex = 0;
    List<SubclipInfo> currentSubclips;
    bool atLeastOneLoop = false;

    void Awake() {
        if (instance == null) {
            instance = this;
        } else {
            Destroy(this);
        }
    }

    void Start() {
        tutorialCanvas = GameObject.Find("TutorialCanvas").GetComponent<Canvas>();
        if (tutorialCanvas != null) {
            infoText = tutorialCanvas.FindComponent<Text>("Info");
            readyText = tutorialCanvas.FindComponent<Text>("ReadyText");
            StartCoroutine(Clips());
        }
    }

    void StartListeningForPlayers() {
        GameModel.instance.nc.CallOnMessageWithSender(
            Message.PlayerReleasedA, CheckinPlayer);
        GameModel.instance.nc.CallOnMessage(
            Message.PlayerPressedLeftBumper, () => nextSlideForceCheat = true);
    }

    List<GameObject> GetPlayers() {
        return (from player in GameModel.instance.GetHumanPlayers()
                select player.gameObject).ToList();
    }

    void ResetCheckin() {
        atLeastOneLoop = false;
        nextSlideForceCheat = false;
        foreach (var player in GetPlayers()) {
            checkin[player.gameObject] = false;
        }
        readyText.text = "";
    }

    void SetReadyText() {
        if (atLeastOneLoop) {
            readyText.text = string.Format("Press (A) to continue ({0}/{1})",
                                           NumberCheckedIn(), GetPlayers().Count);
        }
    }

    void CheckinPlayer(object potentialPlayer) {
        if (!atLeastOneLoop) {
            return;
        }
        var player = potentialPlayer as GameObject;
        if (player != null) {
            checkin[player] = true;
        }
        SetReadyText();
    }

    int NumberCheckedIn() {
        return GetPlayers().Count(player => checkin[player]);
    }

    bool AllCheckedIn() {
        var allPlayers = (from player in GetPlayers() select checkin[player]).All(x => x);
        return (allPlayers && atLeastOneLoop) || nextSlideForceCheat;
    }

    void LoadLiveClip(string clipName) {
        Debug.Log("Loading clip");
        currentSubclipIndex = 0;
        SceneManager.LoadScene(clipName, LoadSceneMode.Additive);
        SetCurrentSubclip();
        foreach (var team in GameModel.instance.teams) {
            team.ResetScore();
        }
    }
    bool clipReloadThisFrame = false;
    IEnumerator Clips() {
        runningLiveClips = true;
        StartListeningForPlayers();
        GameModel.instance.nc.CallOnMessage(Message.RecordingFinished,
                                            () => {
                                                if (!clipReloadThisFrame) {
                                                    ClipReload();
                                                    clipReloadThisFrame = true;
                                                    this.FrameDelayCall(() => clipReloadThisFrame = false, 3);
                                                }
                                            });
        GameModel.instance.nc.CallOnMessage(Message.RecordingInterrupt,
                                            SubclipInterrupt);
        yield return null;
        foreach (var liveClip in liveClips) {
            clipReloadThisFrame = false;
            currentClip = liveClip;
            ResetCheckin();
            currentClipName = liveClip.clipName;
            currentSubclips = liveClip.subclipInfo;
            yield return new WaitForSecondsRealtime(liveClip.preDelay);
            LoadLiveClip(currentClipName);
            yield return null;
            while (!AllCheckedIn()) {
                yield return null;
            }
            yield return null;
            Debug.Log("all checked in");
            TransitionUtility.OneShotFadeTransition(0.3f, 0.2f);
            yield return new WaitForSecondsRealtime(0.15f);
            UnloadCurrentClip();
            yield return null;
            
        }
        TransitionUtility.OneShotFadeTransition(0.1f, 0.4f);
        yield return new WaitForSeconds(0.05f);
        runningLiveClips = false;
        SceneStateController.instance.Load(Scene.Sandbox);
    }

    void SetSubclipText(string text) {
        infoText.text = text;
    }

    void SetCurrentSubclip() {
        if (currentSubclipIndex < currentSubclips.Count) {
            var subclip = currentSubclips[currentSubclipIndex];
            SetSubclipText(subclip.clipText);
        }
    }

    void SubclipInterrupt() {
        if (currentSubclipIndex < currentSubclips.Count) {
            PlayerPuppet.puppetsPause = true;
            var subclip = currentSubclips[currentSubclipIndex];
            this.TimeDelayCall(() => {
                    currentSubclipIndex += 1;
                    SetCurrentSubclip();
                    PlayerPuppet.puppetsPause = false;
                },
                subclip.timeAdjustment);
        }
    }

    void UnloadCurrentClip() {
        var clipObject = GameObject.Find(currentClipName);
        if (clipObject) {
            Destroy(clipObject);
        }
        SceneManager.UnloadScene(currentClipName);
    }

    void ClipReload() {
        Debug.Log("Clip reload");
        var clipName = currentClipName;
        atLeastOneLoop = true;
        SetReadyText();
        this.TimeDelayCall(() => {
                if (currentClipName == clipName) {
                    UnloadCurrentClip();
                    this.RealtimeDelayCall(() => {
                            if (currentClipName == clipName) {
                                LoadLiveClip(clipName);
                            }
                        }, 0.05f);
                }
            }, Mathf.Max(currentClip.postDelay, 0.1f));

        // reason for this value: 0.07f is slightly longer than the 0.05f delay
        // from a few lines up
        float epsilon = 0.07f; 
        float delayBeforeFade = currentClip.postDelay/4;
        float totalTransitionDuration = Mathf.Max(delayBeforeFade + epsilon, 0.1f);
        this.TimeDelayCall(
            () => TransitionUtility.OneShotFadeTransition(totalTransitionDuration * 3, totalTransitionDuration),
            delayBeforeFade);
    }
}

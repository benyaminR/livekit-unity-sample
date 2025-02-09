using System.Collections;
using UnityEngine;
using LiveKit;
using LiveKit.Proto;
using UnityEngine.UI;
using RoomOptions = LiveKit.RoomOptions;
using System.Collections.Generic;
using Application = UnityEngine.Application;
using TMPro;
using System;
using UnityEngine.Networking;
using System.Linq;


[System.Serializable]
public class TokenResponse
{
    public string participantToken;
}

public class LivekitSamples : MonoBehaviour
{
    public string url = "ws://localhost:7880";
    public string tokenServerUrl = "https://cloud-api.livekit.io/api/sandbox/connection-details";
    public string roomName = "testRoom";
    public int frameRate = 30;
    private string token = "";

    private Room room = null;

    private WebCamTexture webCamTexture = null;


    Dictionary<string, GameObject> _videoObjects = new();
    Dictionary<string, GameObject> _audioObjects = new();
    List<RtcVideoSource> _rtcVideoSources = new();
    List<RtcAudioSource> _rtcAudioSources = new();
    List<VideoStream> _videoStreams = new();

    public GridLayoutGroup layoutGroup;

    public TMP_Text statusText;


    public void Start()
    {
        StartCoroutine(MakeCall());
    }

    public void UpdateStatusText(string newText)
    {
        if (statusText != null)
        {
            statusText.text = newText;
        }
    }
    IEnumerator MakeCall()
    {
        yield return RetrieveToken();

        yield return OpenCamera();

        if (room == null)
        {
            room = new Room();
            room.TrackSubscribed += TrackSubscribed;
            room.TrackUnsubscribed += UnTrackSubscribed;
            room.DataReceived += DataReceived;
            var options = new RoomOptions();
            var connect = room.Connect(url, token, options);
            yield return connect;
            if (!connect.IsError)
            {
                Debug.Log("Connected to " + room.Name);
                UpdateStatusText("Connected");
            }
            else
            {
                Debug.Log("Failed to connect!");
            }
        }

        yield return publishMicrophone();
        yield return publishVideo();

    }

    IEnumerator RetrieveToken()
    {
        using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{tokenServerUrl}?roomName={roomName}", "{}"))
        {
            www.SetRequestHeader("X-Sandbox-ID", "nano-vault-22bmk6");
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(www.error);
            }
            else
            {
                Debug.Log("Form upload complete!");
                var jsonResponse = www.downloadHandler.text;
                token = JsonUtility.FromJson<TokenResponse>(jsonResponse).participantToken;
            }
        }
    }

    void CleanUp()
    {
        foreach (var item in _audioObjects)
        {
            var source = item.Value.GetComponent<AudioSource>();
            source.Stop();
            Destroy(item.Value);
        }

        _audioObjects.Clear();

        foreach (var item in _rtcAudioSources)
        {
            item.Stop();
        }


        foreach (var item in _videoObjects)
        {
            RawImage img = item.Value.GetComponent<RawImage>();
            if (img != null)
            {
                img.texture = null;
                Destroy(img);
            }

            Destroy(item.Value);
        }

        foreach (var item in _videoStreams)
        {
            item.Stop();
            item.Dispose();
        }

        foreach (var item in _rtcVideoSources)
        {
            item.Stop();
            item.Dispose();
        }

        _videoObjects.Clear();

        _videoStreams.Clear();
    }


    void AddVideoTrack(RemoteVideoTrack videoTrack)
    {
        Debug.Log("AddVideoTrack " + videoTrack.Sid);

        GameObject imgObject = new GameObject(videoTrack.Sid);

        RectTransform trans = imgObject.AddComponent<RectTransform>();
        trans.localScale = Vector3.one;
        trans.sizeDelta = new Vector2(300, 300);
        trans.rotation = Quaternion.AngleAxis(Mathf.Lerp(0f, 180f, 50), Vector3.forward);

        RawImage image = imgObject.AddComponent<RawImage>();

        var stream = new VideoStream(videoTrack);
        stream.TextureReceived += (tex) =>
        {
            if (image != null)
            {
                image.texture = tex;
            }
        };

        _videoObjects[videoTrack.Sid] = imgObject;

        imgObject.transform.SetParent(layoutGroup.gameObject.transform, false);
        stream.Start();
        StartCoroutine(stream.Update());
        _videoStreams.Add(stream);
    }

    void TrackSubscribed(IRemoteTrack track, RemoteTrackPublication publication, RemoteParticipant participant)
    {
        if (track is RemoteVideoTrack videoTrack)
        {
            AddVideoTrack(videoTrack);
        }
        else if (track is RemoteAudioTrack audioTrack)
        {
            GameObject audObject = new GameObject(audioTrack.Sid);
            var source = audObject.AddComponent<AudioSource>();
            var stream = new AudioStream(audioTrack, source);
            _audioObjects[audioTrack.Sid] = audObject;
        }
    }

    void UnTrackSubscribed(IRemoteTrack track, RemoteTrackPublication publication, RemoteParticipant participant)
    {
        if (track is RemoteVideoTrack videoTrack)
        {
            var imgObject = _videoObjects[videoTrack.Sid];
            if (imgObject != null)
            {
                Destroy(imgObject);
            }
            _videoObjects.Remove(videoTrack.Sid);
        }
        else if (track is RemoteAudioTrack audioTrack)
        {
            var audObject = _audioObjects[audioTrack.Sid];
            if (audObject != null)
            {
                var source = audObject.GetComponent<AudioSource>();
                source.Stop();
                Destroy(audObject);
            }
            _audioObjects.Remove(audioTrack.Sid);
        }
    }

    void DataReceived(byte[] data, Participant participant, DataPacketKind kind, string topic)
    {
        var str = System.Text.Encoding.Default.GetString(data);
        Debug.Log("DataReceived: from " + participant.Identity + ", data " + str);
        UpdateStatusText("DataReceived: from " + participant.Identity + ", data " + str);
    }

    public IEnumerator publishMicrophone()
    {
        Debug.Log("publicMicrophone!");
        // Publish Microphone
        var localSid = "my-audio-source";
        GameObject audObject = new GameObject(localSid);
        _audioObjects[localSid] = audObject;
        var source = audObject.AddComponent<AudioSource>();
        source.clip = Microphone.Start(Microphone.devices[0], true, 2, (int)RtcAudioSource.DefaultSampleRate);
        source.loop = true;

        //var rtcSource = new RtcAudioSource(source);
        Debug.Log($"CreateAudioTrack");
        //var track = LocalAudioTrack.CreateAudioTrack("my-audio-track", rtcSource, room);

        //var options = new TrackPublishOptions();
        //options.AudioEncoding = new AudioEncoding();
        //options.AudioEncoding.MaxBitrate = 64000;
        //options.Source = TrackSource.SourceMicrophone;

        //Debug.Log("PublishTrack!");
        //var publish = room.LocalParticipant.PublishTrack(track, options);
        //yield return publish;

        //if (!publish.IsError)
        //{
        //    Debug.Log("Track published!");
        //}

        //_rtcAudioSources.Add(rtcSource);
        //rtcSource.Start();
        yield return new WaitForEndOfFrame();
    }

    public IEnumerator publishVideo()
    {
        var source = new TextureVideoSource(webCamTexture);

        GameObject imgObject = new GameObject("camera");
        RectTransform trans = imgObject.AddComponent<RectTransform>();
        trans.localScale = Vector3.one;
        trans.sizeDelta = new Vector2(300, 300);
        RawImage image = imgObject.AddComponent<RawImage>();

        image.texture = webCamTexture;

        imgObject.transform.SetParent(layoutGroup.gameObject.transform, false);
        var track = LocalVideoTrack.CreateVideoTrack("my-video-track", source, room);

        var options = new TrackPublishOptions();
        options.VideoCodec = VideoCodec.Vp8;
        var videoCoding = new VideoEncoding();
        videoCoding.MaxBitrate = 512000;
        videoCoding.MaxFramerate = frameRate;
        options.VideoEncoding = videoCoding;
        options.Simulcast = true;
        options.Source = TrackSource.SourceCamera;

        var publish = room.LocalParticipant.PublishTrack(track, options);
        yield return publish;

        if (!publish.IsError)
        {
            Debug.Log("Track published!");
        }

        source.Start();
        StartCoroutine(source.Update());
        _rtcVideoSources.Add(source);
    }


    public IEnumerator OpenCamera()
    {
        int maxl = Screen.width;
        if (Screen.height > Screen.width)
        {
            maxl = Screen.height;
        }

        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            if (webCamTexture != null)
            {
                webCamTexture.Stop();
            }

            int i = 0;
            while (WebCamTexture.devices.Length <= 0 && 1 < 300)
            {
                yield return new WaitForEndOfFrame();
                i++;
            }
            WebCamDevice[] devices = WebCamTexture.devices;
            if (WebCamTexture.devices.Length <= 0)
            {
                Debug.LogError("No camera device available, please check");
            }
            else
            {
                var deviceName = string.Empty;
                if (devices.Any(x => x.isFrontFacing))
                {
                    deviceName = devices.First(x => x.isFrontFacing).name;
                }
                else
                {
                    deviceName = devices[0].name;
                }
                webCamTexture = new WebCamTexture(deviceName, maxl, maxl == Screen.height ? Screen.width : Screen.height, frameRate)
                {
                    wrapMode = TextureWrapMode.Repeat
                };

                webCamTexture.Play();
            }

        }
        else
        {
            Debug.LogError("Camera permission not obtained");
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (webCamTexture != null)
        {
            if (pause)
            {
                webCamTexture.Pause();
            }
            else
            {
                webCamTexture.Play();
            }
        }

    }


    private void OnDestroy()
    {
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
        }
    }
}

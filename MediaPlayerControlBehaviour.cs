// You need to define AVPRO_PACKAGE_TIMELINE manually to use this script
// We could set up the asmdef to reference the package, but the package doesn't 
// existing in Unity 2017 etc, and it throws an error due to missing reference
//#define AVPRO_PACKAGE_TIMELINE
#if (UNITY_2018_1_OR_NEWER && AVPRO_PACKAGE_TIMELINE)
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

//-----------------------------------------------------------------------------
// Copyright 2020-2021 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProVideo.Playables
{
    public class MediaPlayerControlBehaviour : PlayableBehaviour
    {
        public MediaPlayerControlAsset controlAsset;
        public MediaPlayer mediaPlayer = null;

        public MediaReference mediaReference = null;
        public float audioVolume = 1f;
        public double startTime = 0.0;
        public bool loop = false;
        public bool pauseOnEnd = true;
        public bool frameAccurateSeek = false;
        public MethodInfo stopRenderCoroutine = null;
        public double preloadTime = 0.3;
        public bool scrubInEditor = false;
        public bool enforceSyncOnDrift = false;
        public double driftTolerance = 0.5;
        
        private MediaReference _originalMediaReference = null;
        private bool _originalLooping;
        private bool _preparing = false;
        private float _lastFrameTime = 0;
        private double clipLength {
            get => controlAsset.clipLength;
            set => controlAsset.clipLength = value;
        } 

        void DoSeek(double time)
        {
            if (frameAccurateSeek)
            {
                mediaPlayer.Control.Seek(startTime + time);
                WaitForFinishSeek();
            }
            else
            {
                mediaPlayer.Control.SeekFast(startTime + time);
            }
        }

        public void PrepareMedia()
        {
            if (mediaPlayer == null) return;

            if (_preparing)
                return;

            if ( !Application.isPlaying && !scrubInEditor ) {
                return;
            }
            if (mediaPlayer.MediaSource == MediaSource.Reference ? mediaPlayer.MediaReference != null : mediaPlayer.MediaPath.Path != "")
            {
                _originalLooping = mediaPlayer.Loop;
                mediaPlayer.Loop = loop;
                if ( mediaReference != mediaPlayer.MediaReference || !mediaPlayer.MediaOpened ) {
                    if ( mediaReference != null && mediaReference != mediaPlayer.MediaReference ) {
                        _originalMediaReference = mediaPlayer.MediaReference;
                        mediaPlayer.OpenMedia(mediaReference, false);
                    } else {
                        mediaPlayer.OpenMedia(mediaPlayer.MediaReference, false);
                    }
                }

                if (frameAccurateSeek) stopRenderCoroutine?.Invoke(mediaPlayer, null);

                _preparing = true;
            }
        }

        public void StopMedia()
        {
            if(mediaPlayer != null && mediaPlayer.Control != null && mediaReference == mediaPlayer.MediaReference &&  mediaPlayer.Control.IsPlaying())
            {
                mediaPlayer.Control.Stop();
            }
            _preparing = false;
        }

        public override void OnPlayableDestroy(Playable playable) {
            if ( Application.isPlaying && _originalMediaReference != null ) {
                mediaPlayer.OpenMedia(_originalMediaReference, autoPlay: false);
            }
            if ( !Application.isPlaying && !scrubInEditor ) {
                return;
            }
            if ( mediaPlayer != null && mediaPlayer.Control != null ) {
                mediaPlayer.Loop = _originalLooping;
            }
        }

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (mediaPlayer != null)
            {
                if ( !Application.isPlaying && !scrubInEditor ) {
                    return;
                }
                if ( !_preparing // fix race condition where OnBehaviourPlay is run before PrepareMedia, or if else unprepared
                     // && playable.GetTime() == 0 
                     && info.evaluationType == FrameData.EvaluationType.Playback
                     && info.effectivePlayState == PlayState.Playing ) {
                    PrepareMedia();
                }
                mediaPlayer.Play();
                if (mediaPlayer.Control != null)
                {
                    DoSeek(playable.GetTime());
                }
                // WaitForFinishSeek();

                if (!Application.isPlaying || frameAccurateSeek)
                    mediaPlayer.Pause();
            }
            _preparing = false;
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (mediaPlayer != null)
            {
                if (pauseOnEnd)
                {
                    mediaPlayer.Pause();
                }
            }
            _preparing = false;
        }

        void WaitForFinishSeek()
        {
            Stopwatch timer = Stopwatch.StartNew(); //never get stuck
            while (mediaPlayer.Control.IsSeeking() && timer.ElapsedMilliseconds < 5000)
            {
                mediaPlayer.Player.Update();
                mediaPlayer.Player.EndUpdate();
            }
            mediaPlayer.Player.Render();
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (mediaPlayer != null) 
            {
                if ( mediaPlayer.Control != null ) {
                    double _clipLength = 0;
                    TimeRanges ranges = mediaPlayer.Control.GetSeekableTimes();
                    for ( int i = 0; i < ranges.Count; i++ ) {
                        _clipLength = Math.Max(_clipLength, ranges[i].EndTime);
                    }
                    if ( 0 < _clipLength && mediaReference == mediaPlayer.MediaReference && !_preparing ) {
                        clipLength = _clipLength;
                    }

                    if ( frameAccurateSeek ) {
                        mediaPlayer.Control.Seek(startTime + playable.GetTime());
                        WaitForFinishSeek();
                    } else if ( !Application.isPlaying && scrubInEditor ) {
                        mediaPlayer.Control.SeekFast(startTime + playable.GetTime());
                        mediaPlayer.Player.Update();
                        mediaPlayer.Player.EndUpdate();
                        mediaPlayer.Player.Render();
                    }

                    if ( enforceSyncOnDrift && Application.isPlaying && Time.time - _lastFrameTime > 1f ) {
                        _lastFrameTime = Time.time;
                        double adjustedPlayableTime = ((startTime + playable.GetTime()) % clipLength); // todo: still assumes 1.0 normal playback speed
                        if ( mediaPlayer.Control.IsLooping() && 0 < clipLength ) {
                            adjustedPlayableTime %= clipLength;
                        }
                        double offset = mediaPlayer.Control.GetCurrentTime() - adjustedPlayableTime;
                        if ( offset < -clipLength * .9 ) { // if we're almost full length behind we've probably looped the video
                            offset += clipLength;
                        }
                        // Debug.Log("Video drift: " + offset);
                        if ( Math.Abs(offset) > driftTolerance ) {
                            Debug.Log("Video drift correction: " + offset);
                            mediaPlayer.Control.Seek(adjustedPlayableTime);
                        }
                    }
                }
            }
        }
    }
}
#endif

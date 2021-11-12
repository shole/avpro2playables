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
        public MediaPlayer mediaPlayer = null;

        public MediaReference mediaReference = null;
        public float audioVolume = 1f;
        public double startTime = 0.0;
        public bool pauseOnEnd = true;
        public bool frameAccurateSeek = false;
        public MethodInfo stopRenderCoroutine = null;
        public double preloadTime = 0.3;
        public bool scrubInEditor = false;
        public bool enforceSyncOnDrift = false;
        public double driftTolerance = 0.5;
        
        private MediaReference _originalMediaReference = null;
        private bool _preparing = false;
        private float _lastFrameTime = 0;

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

            if (mediaPlayer.MediaOpened || _preparing)
                return;

            if ( !Application.isPlaying && !scrubInEditor ) {
                return;
            }
            if (mediaPlayer.MediaSource == MediaSource.Reference ? mediaPlayer.MediaReference != null : mediaPlayer.MediaPath.Path != "")
            {
                if(mediaReference != null && mediaReference != mediaPlayer.MediaReference) {
                    _originalMediaReference = mediaPlayer.MediaReference;
                    mediaPlayer.OpenMedia(mediaReference, false);
                }
                else
                {
                    mediaPlayer.OpenMedia(mediaPlayer.MediaReference, false);
                }

                if (frameAccurateSeek) stopRenderCoroutine?.Invoke(mediaPlayer, null);

                _preparing = true;
            }
        }

        public void StopMedia()
        {
            if(mediaPlayer != null && mediaPlayer.Control != null)
            {
                mediaPlayer.Control.Stop();
            }
            _preparing = false;
        }

        public override void OnPlayableDestroy(Playable playable) {
            if ( _originalMediaReference != null ) {
                mediaPlayer.OpenMedia(_originalMediaReference, autoPlay: false);
            }
        }

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (mediaPlayer != null)
            {
                if ( !Application.isPlaying && !scrubInEditor ) {
                    return;
                }
                if ( !_preparing // fix race condition where OnBehaviourPlay is run before PrepareMedia
                     && playable.GetTime() == 0
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
                if (mediaPlayer.Control != null)
                {
                    if (frameAccurateSeek)
                    {
                        mediaPlayer.Control.Seek(startTime + playable.GetTime());
                        WaitForFinishSeek();
                    }
                    else if( !Application.isPlaying && scrubInEditor )
                    {
                        mediaPlayer.Control.SeekFast(startTime + playable.GetTime());
                        mediaPlayer.Player.Update();
                        mediaPlayer.Player.EndUpdate();
                        mediaPlayer.Player.Render();
                    }
                    if ( enforceSyncOnDrift && Time.time - _lastFrameTime > 1f ) {
                        _lastFrameTime = Time.time;
                        double offset = mediaPlayer.Control.GetCurrentTime() - (startTime + playable.GetTime());
                        // Debug.Log("Video drift: "+offset );
                        if ( Math.Abs(offset) > driftTolerance ) {
                            Debug.Log("Video drift correction: "+offset);
                            mediaPlayer.Control.Seek(startTime + playable.GetTime());
                        }
                    }
                }
            }
        }
    }
}
#endif

﻿// You need to define AVPRO_PACKAGE_TIMELINE manually to use this script
// We could set up the asmdef to reference the package, but the package doesn't 
// existing in Unity 2017 etc, and it throws an error due to missing reference
#define AVPRO_PACKAGE_TIMELINE
#if (UNITY_2018_1_OR_NEWER && AVPRO_PACKAGE_TIMELINE)
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections.Generic;
using System;

//-----------------------------------------------------------------------------
// Copyright 2020-2021 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProVideo.Playables
{
	public class MediaPlayerControlMixerBehaviour : PlayableBehaviour
	{
		public float audioVolume = 1f;
		public string videoPath = null;
		public IEnumerable<TimelineClip> clips;
		public PlayableDirector director;
		public override void ProcessFrame(Playable playable, FrameData info, object playerData)
		{
			MediaPlayer mediaPlayer = playerData as MediaPlayer;
			float finalVolume = 0f;

			if (!mediaPlayer)
				return;

			int inputCount = playable.GetInputCount(); //get the number of all clips on this track
			for (int i = 0; i < inputCount; i++)
			{
				float inputWeight = playable.GetInputWeight(i);
				ScriptPlayable<MediaPlayerControlBehaviour> inputPlayable = (ScriptPlayable<MediaPlayerControlBehaviour>)playable.GetInput(i);
				MediaPlayerControlBehaviour input = inputPlayable.GetBehaviour();
				
				// Use the above variables to process each frame of this playable.
				finalVolume += input.audioVolume * inputWeight;
			}

			if (mediaPlayer != null)
			{
				mediaPlayer.AudioVolume = finalVolume;
				if (mediaPlayer.Control != null)
				{
					mediaPlayer.Control.SetVolume(finalVolume);
				}
			}

			//prepare clips
			if (clips == null)
				return;

			int inputPort = 0;
			foreach (TimelineClip clip in clips)
			{
				ScriptPlayable<MediaPlayerControlBehaviour> scriptPlayable = (ScriptPlayable<MediaPlayerControlBehaviour>)playable.GetInput(inputPort);

				MediaPlayerControlBehaviour behaviour = scriptPlayable.GetBehaviour();

				if ( behaviour != null ) {
					double preloadTime = Math.Max(0.0, behaviour.preloadTime);
					if ( !Application.isPlaying && behaviour.scrubInEditor ) {
						if ( (director.time < clip.start || clip.start + clip.end < director.time) ) { // stop in editor scrubbing.. 
							behaviour.StopMedia();
						} else if ( clip.start <= director.time && director.time < clip.end ) { // preload while in preload window.. else play call will force load
							behaviour.PrepareMedia();
						}
					} else if ( clip.start - preloadTime <= director.time && director.time < clip.start ) { // preload while in preload window.. else play call will force load
						if ( behaviour.mediaPlayer!=null && behaviour.mediaPlayer.Control!=null && !behaviour.mediaPlayer.Control.IsPlaying() ) { // only preload if player not already playing
							behaviour.PrepareMedia();
						}
					}
				}

				++inputPort;
			}

		}
	}
}
#endif

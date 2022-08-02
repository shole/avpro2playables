// You need to define AVPRO_PACKAGE_TIMELINE manually to use this script
// We could set up the asmdef to reference the package, but the package doesn't 
// existing in Unity 2017 etc, and it throws an error due to missing reference
#define AVPRO_PACKAGE_TIMELINE
#if (UNITY_2018_1_OR_NEWER && AVPRO_PACKAGE_TIMELINE)
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections.Generic;
using System.Reflection;
using Object = UnityEngine.Object;

//-----------------------------------------------------------------------------
// Copyright 2020-2021 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProVideo.Playables {
	[System.Serializable]
	public class MediaPlayerControlAsset : PlayableAsset {
		public bool scrubInEditor = false;
		public double clipLength = -1;
		public Object binding {
			get;
			set;
		}
		//public ExposedReference<MediaPlayer> mediaPlayer;

		[Space]
		public MediaReference mediaReference;
		[Space]
		[Min(0f)]
		public float startTime = 0.0f;
		public bool loop = false;
		public bool pauseOnEnd = true;
		[Space]
		[Range(0f, 1f)]
		public float audioVolume = 1f;
		[Space]
		public double preloadTime = 0.3;
		public bool frameAccurateSeek = false;
		[Space]
		public bool enforceSyncOnDrift = false;
		public double driftTolerance = 0.5;
		public bool waitForSync = false;

		private ScriptPlayable<MediaPlayerControlBehaviour> _playable;

		private void OnValidate() {
			UpdatePlayable();
		}

		void UpdatePlayable() {
			var behaviour = _playable.GetBehaviour();
			if ( behaviour == null ) {
				return;
			}
			behaviour.controlAsset = this;
			//behaviour.mediaPlayer = mediaPlayer.Resolve(graph.GetResolver());
			behaviour.audioVolume = audioVolume;
			behaviour.pauseOnEnd = pauseOnEnd;
			behaviour.startTime = (double)startTime;
			behaviour.mediaReference = mediaReference;
			behaviour.mediaPlayer = (MediaPlayer)binding;
			behaviour.frameAccurateSeek = frameAccurateSeek;
			behaviour.stopRenderCoroutine = behaviour.mediaPlayer.GetType().GetMethod("StopRenderCoroutine", BindingFlags.NonPublic | BindingFlags.Instance);
			behaviour.preloadTime = preloadTime;
			behaviour.scrubInEditor = scrubInEditor;
			behaviour.enforceSyncOnDrift = enforceSyncOnDrift;
			behaviour.driftTolerance = driftTolerance;
			behaviour.waitForSync = waitForSync;
			behaviour.loop = loop;
		}

		public override double duration { // this is used as the default duration of the clip when created
			get {
				return clipLength <= 0 ? 30 : clipLength - startTime;
			}
		}

		public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) {
			_playable = ScriptPlayable<MediaPlayerControlBehaviour>.Create(graph);
			UpdatePlayable();
			return _playable;
		}
	}
}
#endif

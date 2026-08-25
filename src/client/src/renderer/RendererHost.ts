import { DungeonRenderer } from '$renderer/DungeonRenderer';
import { toRenderModel } from '$renderer/RenderModel';
import { AmbientAudioManager } from '$renderer/AmbientAudio';
import { UnaccountedAudioManager } from '$renderer/UnaccountedAudioManager';
import type { SubtitleEntry } from '$renderer/SubtitleSystem';
import { loadDisplaySettings, type DisplaySettings } from '$config/displaySettings';
import { loadAccessibilitySettings, type AccessibilitySettings } from '$config/accessibilitySettings';
import type { GameState } from '$shared/types/game';

/**
 * Isolates the 3D renderer + ambient/unaccounted audio lifecycle behind a single host.
 * Constructed with the container element once the DOM is ready; driven each frame by
 * update(state). Display/accessibility/audio settings are applied through dedicated
 * methods so the UI shell never touches the renderer or audio managers directly.
 */
export class RendererHost {
  private readonly renderer: DungeonRenderer;
  private readonly audioManager = new AmbientAudioManager();
  private readonly unaccountedAudio = new UnaccountedAudioManager(this.audioManager);
  private currentSubtitles: SubtitleEntry[] = [];

  constructor(container: HTMLElement) {
    this.renderer = new DungeonRenderer(container);
    const display = loadDisplaySettings();
    this.renderer.setFov(display.fov);
    this.renderer.setResolutionScale(display.resolutionScale);
    this.renderer.setReduceMotion(loadAccessibilitySettings().reduceMotion);
  }

  /** Push the latest game state to the renderer and audio managers, refreshing subtitles. */
  update(state: GameState | null) {
    if (state) {
      this.renderer.updateState(toRenderModel(state));
    }
    this.audioManager.update(state?.dungeonType);
    this.unaccountedAudio.update(state);
    this.currentSubtitles = this.unaccountedAudio.subtitles.getActive();
  }

  get subtitleEntries(): SubtitleEntry[] {
    return this.currentSubtitles;
  }

  /** Shared ambient audio manager, also driven by the overworld map for faction motifs. */
  get ambientAudio(): AmbientAudioManager {
    return this.audioManager;
  }

  applyDisplaySettings(d: DisplaySettings) {
    this.renderer.setFov(d.fov);
    this.renderer.setResolutionScale(d.resolutionScale);
  }

  applyAccessibilitySettings(a: AccessibilitySettings) {
    this.renderer.setReduceMotion(a.reduceMotion);
  }

  setAudioEnabled(enabled: boolean) {
    this.audioManager.setEnabled(enabled);
    this.unaccountedAudio.setEnabled(enabled);
  }
}

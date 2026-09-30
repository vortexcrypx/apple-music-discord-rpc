import Foundation
import AppKit

public struct CurrentTrack {
    public let title: String
    public let artist: String
    public let album: String
    public let isPlaying: Bool
    public let position: Double
    public let duration: Double
}

public protocol MusicMonitorDelegate: AnyObject {
    func musicMonitorDidUpdateTrack(_ monitor: MusicMonitor, track: CurrentTrack?)
}

public final class MusicMonitor {
    public weak var delegate: MusicMonitorDelegate?
    private var isObserving = false

    public init() {}

    public func start() {
        guard !isObserving else { return }
        isObserving = true

        DistributedNotificationCenter.default().addObserver(
            self,
            selector: #selector(handlePlayerInfoNotification(_:)),
            name: NSNotification.Name("com.apple.Music.playerInfo"),
            object: nil
        )

        // Initial check
        checkCurrentTrack()
    }

    public func stop() {
        DistributedNotificationCenter.default().removeObserver(self)
        isObserving = false
    }

    @objc private func handlePlayerInfoNotification(_ notification: Notification) {
        guard let info = notification.userInfo as? [String: Any] else { return }

        let state = info["Player State"] as? String ?? ""
        let isPlaying = (state == "Playing")

        if !isPlaying {
            delegate?.musicMonitorDidUpdateTrack(self, track: nil)
            return
        }

        let title = info["Name"] as? String ?? ""
        let artist = info["Artist"] as? String ?? ""
        let album = info["Album"] as? String ?? ""
        let totalTimeMs = (info["Total Time"] as? NSNumber)?.doubleValue ?? 0.0
        let duration = totalTimeMs / 1000.0

        let position = queryPlayerPosition()

        let track = CurrentTrack(
            title: title,
            artist: artist,
            album: album,
            isPlaying: true,
            position: position,
            duration: duration
        )
        delegate?.musicMonitorDidUpdateTrack(self, track: track)
    }

    public func checkCurrentTrack() {
        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            guard let self = self else { return }
            let scriptSource = """
            if application "Music" is running then
                tell application "Music"
                    set pState to (player state as string)
                    if pState is "playing" then
                        set tName to name of current track
                        set tArtist to artist of current track
                        set tAlbum to album of current track
                        set tPos to player position
                        set tDur to duration of current track
                        return tName & "|||" & tArtist & "|||" & tAlbum & "|||" & (tPos as string) & "|||" & (tDur as string)
                    end if
                end tell
            end if
            return ""
            """

            guard let appleScript = NSAppleScript(source: scriptSource) else { return }
            var errorDict: NSDictionary?
            let resultDesc = appleScript.executeAndReturnError(&errorDict)
            let result = resultDesc.stringValue ?? ""

            if result.isEmpty {
                DispatchQueue.main.async {
                    self.delegate?.musicMonitorDidUpdateTrack(self, track: nil)
                }
                return
            }

            let parts = result.components(separatedBy: "|||")
            if parts.count >= 5 {
                let track = CurrentTrack(
                    title: parts[0],
                    artist: parts[1],
                    album: parts[2],
                    isPlaying: true,
                    position: Double(parts[3]) ?? 0.0,
                    duration: Double(parts[4]) ?? 0.0
                )
                DispatchQueue.main.async {
                    self.delegate?.musicMonitorDidUpdateTrack(self, track: track)
                }
            }
        }
    }

    private func queryPlayerPosition() -> Double {
        let scriptSource = """
        if application "Music" is running then
            tell application "Music" to return player position
        else
            return 0
        end if
        """
        guard let script = NSAppleScript(source: scriptSource) else { return 0.0 }
        var errorDict: NSDictionary?
        let result = script.executeAndReturnError(&errorDict)
        return result.doubleValue
    }
}

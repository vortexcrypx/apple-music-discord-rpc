import AppKit
import Foundation

final class AppDelegate: NSObject, NSApplicationDelegate, MusicMonitorDelegate {
    private var statusItem: NSStatusItem!
    private let discordIpc = DiscordIPC()
    private let musicMonitor = MusicMonitor()
    private var isPresenceEnabled = true
    private var currentTrackInfo: CurrentTrack?
    private var statusMenuItem: NSMenuItem!
    private var toggleMenuItem: NSMenuItem!

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)

        setupStatusBar()
        musicMonitor.delegate = self
        musicMonitor.start()

        _ = discordIpc.connect()
    }

    private func setupStatusBar() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)

        if let button = statusItem.button {
            button.title = "🎵"
            button.toolTip = "Apple Music Discord RPC"
        }

        let menu = NSMenu()

        statusMenuItem = NSMenuItem(title: "Apple Music: Idle", action: nil, keyEquivalent: "")
        statusMenuItem.isEnabled = false
        menu.addItem(statusMenuItem)

        menu.addItem(NSMenuItem.separator())

        toggleMenuItem = NSMenuItem(title: "Discord Presence: ON", action: #selector(togglePresence), keyEquivalent: "p")
        toggleMenuItem.target = self
        menu.addItem(toggleMenuItem)

        menu.addItem(NSMenuItem.separator())

        let quitItem = NSMenuItem(title: "Quit Apple Music RPC", action: #selector(quitApp), keyEquivalent: "q")
        quitItem.target = self
        menu.addItem(quitItem)

        statusItem.menu = menu
    }

    @objc private func togglePresence() {
        isPresenceEnabled.toggle()
        toggleMenuItem.title = isPresenceEnabled ? "Discord Presence: ON" : "Discord Presence: OFF"

        if isPresenceEnabled {
            if let track = currentTrackInfo {
                updateDiscordPresence(with: track)
            } else {
                musicMonitor.checkCurrentTrack()
            }
        } else {
            discordIpc.clearPresence()
        }
    }

    @objc private func quitApp() {
        discordIpc.clearPresence()
        discordIpc.disconnect()
        musicMonitor.stop()
        NSApp.terminate(nil)
    }

    func musicMonitorDidUpdateTrack(_ monitor: MusicMonitor, track: CurrentTrack?) {
        currentTrackInfo = track

        DispatchQueue.main.async { [weak self] in
            guard let self = self else { return }

            if let track = track, track.isPlaying {
                self.statusMenuItem.title = "Playing: \(track.title) - \(track.artist)"
                if self.isPresenceEnabled {
                    self.updateDiscordPresence(with: track)
                }
            } else {
                self.statusMenuItem.title = "Apple Music: Idle"
                if self.isPresenceEnabled {
                    self.discordIpc.clearPresence()
                }
            }
        }
    }

    private func updateDiscordPresence(with track: CurrentTrack) {
        let now = Int64(Date().timeIntervalSince1970)
        let elapsed = Int64(track.position)
        let duration = Int64(track.duration)

        let startTimestamp = now - elapsed
        let endTimestamp = (duration > 0) ? (startTimestamp + duration) : nil

        MetadataService.shared.resolve(title: track.title, artist: track.artist, album: track.album) { [weak self] metadata in
            guard let self = self else { return }

            self.discordIpc.update(
                details: track.title,
                state: "by \(track.artist)",
                largeImage: metadata.artworkUrl,
                largeText: track.album.isEmpty ? track.title : track.album,
                startTimestamp: startTimestamp,
                endTimestamp: endTimestamp,
                buttonUrl: metadata.songUrl
            )
        }
    }
}

// Extension to bridge updatePresence call
extension DiscordIPC {
    func update(
        details: String,
        state: String,
        largeImage: String?,
        largeText: String?,
        startTimestamp: Int64?,
        endTimestamp: Int64?,
        buttonUrl: String?
    ) {
        self.updatePresence(
            details: details,
            state: state,
            largeImage: largeImage,
            largeText: largeText,
            startTimestamp: startTimestamp,
            endTimestamp: endTimestamp,
            buttonUrl: buttonUrl
        )
    }
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.run()

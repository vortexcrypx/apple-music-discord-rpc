import Foundation

public final class DiscordIPC {
    private let clientId: String
    private var socketFd: Int32 = -1
    private var isConnected = false

    public init(clientId: String = "773825528921849856") {
        self.clientId = clientId
    }

    public func connect() -> Bool {
        if isConnected && socketFd >= 0 { return true }

        let possiblePaths = getSocketPaths()
        for path in possiblePaths {
            if connectToSocket(path: path) {
                isConnected = true
                if performHandshake() {
                    return true
                } else {
                    disconnect()
                }
            }
        }
        return false
    }

    public func disconnect() {
        if socketFd >= 0 {
            close(socketFd)
            socketFd = -1
        }
        isConnected = false
    }

    public func updatePresence(
        details: String,
        state: String,
        largeImage: String?,
        largeText: String?,
        startTimestamp: Int64?,
        endTimestamp: Int64?,
        buttonUrl: String?
    ) {
        guard connect() else { return }

        var activity: [String: Any] = [
            "name": "Apple Music",
            "type": 2, // Listening
            "details": details,
            "state": state
        ]

        var timestamps: [String: Any] = [:]
        if let start = startTimestamp {
            timestamps["start"] = start
        }
        if let end = endTimestamp {
            timestamps["end"] = end
        }
        if !timestamps.isEmpty {
            activity["timestamps"] = timestamps
        }

        var assets: [String: Any] = [:]
        if let img = largeImage, !img.isEmpty {
            assets["large_image"] = img
        } else {
            assets["large_image"] = "app_logo"
        }
        if let txt = largeText, !txt.isEmpty {
            assets["large_text"] = txt
        }
        activity["assets"] = assets

        if let url = buttonUrl, !url.isEmpty {
            activity["buttons"] = [
                ["label": "Listen on Apple Music", "url": url]
            ]
        }

        let payload: [String: Any] = [
            "cmd": "SET_ACTIVITY",
            "args": [
                "pid": ProcessInfo.processInfo.processIdentifier,
                "activity": activity
            ],
            "nonce": UUID().uuidString
        ]

        sendFrame(opcode: 1, payload: payload)
    }

    public func clearPresence() {
        guard isConnected else { return }
        let payload: [String: Any] = [
            "cmd": "SET_ACTIVITY",
            "args": [
                "pid": ProcessInfo.processInfo.processIdentifier,
                "activity": NSNull()
            ],
            "nonce": UUID().uuidString
        ]
        sendFrame(opcode: 1, payload: payload)
    }

    private func performHandshake() -> Bool {
        let handshake: [String: Any] = [
            "v": 1,
            "client_id": clientId
        ]
        guard sendFrame(opcode: 0, payload: handshake) else { return false }

        // Read response
        var header = [UInt8](repeating: 0, count: 8)
        let bytesRead = recv(socketFd, &header, 8, 0)
        return bytesRead == 8
    }

    private func sendFrame(opcode: UInt32, payload: [String: Any]) -> Bool {
        guard let jsonData = try? JSONSerialization.data(withJSONObject: payload, options: []) else {
            return false
        }

        let length = UInt32(jsonData.count)
        var packet = Data()
        var op = opcode.littleEndian
        var len = length.littleEndian

        withUnsafeBytes(of: &op) { packet.append(contentsOf: $0) }
        withUnsafeBytes(of: &len) { packet.append(contentsOf: $0) }
        packet.append(jsonData)

        let sent = packet.withUnsafeBytes { ptr -> Int in
            guard let baseAddress = ptr.baseAddress else { return -1 }
            return send(socketFd, baseAddress, packet.count, 0)
        }

        if sent <= 0 {
            disconnect()
            return false
        }
        return true
    }

    private func getSocketPaths() -> [String] {
        var paths = [String]()
        let envs = ["TMPDIR", "TEMP", "TMP"]
        for env in envs {
            if let dir = ProcessInfo.processInfo.environment[env] {
                for i in 0..<10 {
                    paths.append("\(dir)/discord-ipc-\(i)")
                    paths.append("\(dir)discord-ipc-\(i)")
                }
            }
        }
        for i in 0..<10 {
            paths.append("/tmp/discord-ipc-\(i)")
            paths.append("/var/tmp/discord-ipc-\(i)")
        }
        return paths
    }

    private func connectToSocket(path: String) -> Bool {
        let fd = socket(AF_UNIX, SOCK_STREAM, 0)
        guard fd >= 0 else { return false }

        var addr = sockaddr_un()
        addr.sun_family = sa_family_t(AF_UNIX)

        let pathBytes = path.utf8CString
        guard pathBytes.count < MemoryLayout.size(ofValue: addr.sun_path) else {
            close(fd)
            return false
        }

        _ = withUnsafeMutablePointer(to: &addr.sun_path.0) { ptr in
            pathBytes.withUnsafeBufferPointer { buffer in
                memcpy(ptr, buffer.baseAddress!, buffer.count)
            }
        }

        let addrLen = socklen_t(MemoryLayout<sockaddr_un>.size)
        let result = withUnsafePointer(to: &addr) { ptr -> Int32 in
            ptr.withMemoryRebound(to: sockaddr.self, capacity: 1) { saPtr in
                Darwin.connect(fd, saPtr, addrLen)
            }
        }

        if result == 0 {
            self.socketFd = fd
            return true
        } else {
            close(fd)
            return false
        }
    }

    deinit {
        disconnect()
    }
}

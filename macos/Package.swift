// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "AppleMusicDiscordRPC",
    platforms: [
        .macOS(.v11)
    ],
    products: [
        .executable(
            name: "AppleMusicDiscordRPC",
            targets: ["AppleMusicDiscordRPC"]
        )
    ],
    targets: [
        .executableTarget(
            name: "AppleMusicDiscordRPC",
            path: "Sources/AppleMusicDiscordRPC"
        )
    ]
)

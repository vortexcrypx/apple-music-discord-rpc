import Foundation

public struct TrackMetadata {
    public let artworkUrl: String?
    public let songUrl: String?
}

public final class MetadataService {
    public static let shared = MetadataService()
    private var cache = [String: TrackMetadata]()
    private let queue = DispatchQueue(label: "com.applemusicrpc.metadata")

    private init() {}

    public func resolve(title: String, artist: String, album: String, completion: @escaping (TrackMetadata) -> Void) {
        let key = "\(title.lowercased())|\(artist.lowercased())"

        queue.sync {
            if let cached = cache[key] {
                completion(cached)
                return
            }
        }

        let query = "\(title) \(artist)".addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? ""
        guard let url = URL(string: "https://itunes.apple.com/search?term=\(query)&media=music&entity=song&limit=1") else {
            completion(TrackMetadata(artworkUrl: nil, songUrl: nil))
            return
        }

        URLSession.shared.dataTask(with: url) { [weak self] data, _, error in
            guard let self = self, let data = data, error == nil else {
                completion(TrackMetadata(artworkUrl: nil, songUrl: nil))
                return
            }

            var artwork: String? = nil
            var songUrl: String? = nil

            if let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
               let results = json["results"] as? [[String: Any]],
               let first = results.first {
                if let art = first["artworkUrl100"] as? String {
                    artwork = art.replacingOccurrences(of: "100x100bb.jpg", with: "600x600bb.jpg")
                }
                songUrl = first["trackViewUrl"] as? String
            }

            let result = TrackMetadata(artworkUrl: artwork, songUrl: songUrl)
            self.queue.async {
                self.cache[key] = result
            }
            completion(result)
        }.resume()
    }
}

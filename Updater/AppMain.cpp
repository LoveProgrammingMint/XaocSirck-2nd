#include "Base.hpp"

static std::vector<std::string> readLines(const std::string& filename) {
    std::vector<std::string> lines;
    std::ifstream file(filename);
    std::string line;

    while (std::getline(file, line)) {
        lines.push_back(line);
    }
    return lines;
}

static std::pair<std::string, std::string> splitOnce(const std::string& s, char delim = ';') {
    auto pos = s.find(delim);
    if (pos == std::string::npos) {
        return { s, "" };
    }
    return {
        s.substr(0, pos),
        s.substr(pos + 1)
    };
}

int main(int argc, char* argv[])
{
    std::vector<std::string> files =
        readLines(std::string((argv[1])) + "/update_list.updatelist");

    for (size_t i = 0; i < files.size(); i++)
    {
        auto [src, dst] = splitOnce(files[i]);
        std::filesystem::rename(src, dst);
    }

	return 0;
}

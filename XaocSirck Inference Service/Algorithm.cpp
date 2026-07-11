#include "Algorithm.hpp"
#include <algorithm>
#include <cmath>

std::vector<Single> Algorithm::Softmax(const std::vector<Single>& input)
{
    if (input.empty())
    {
        return {};
    }

    Single maxValue = *std::max_element(input.begin(), input.end());
    std::vector<Single> output(input.size());
    Single sum = 0.0f;

    for (size_t i = 0; i < input.size(); ++i)
    {
        output[i] = std::exp(input[i] - maxValue);
        sum += output[i];
    }

    Single inverseSum = 1.0f / sum;

    for (size_t i = 0; i < input.size(); ++i)
    {
        output[i] *= inverseSum;
    }

    return output;
}

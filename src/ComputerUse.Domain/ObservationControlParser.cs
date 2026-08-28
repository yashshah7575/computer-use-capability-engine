namespace ComputerUse.Domain;

/// <summary>
/// Parses CONTROLS blocks produced by <see cref="ObservationFormatter"/>.
/// </summary>
public static class ObservationControlParser
{
    public static IReadOnlyList<ObservedControl> Parse(string observation)
    {
        var list = new List<ObservedControl>();
        ObservedControl? current = null;
        foreach (var raw in (observation ?? "").Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                if (current is not null)
                    list.Add(current);
                current = new ObservedControl();
                continue;
            }

            if (current is null)
                continue;
            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            var key = line[..eq];
            var value = line[(eq + 1)..];
            switch (key)
            {
                case "tag": current.Tag = value; break;
                case "role": current.Role = value; break;
                case "name": current.Name = value; break;
                case "text": current.Text = value; break;
                case "label": current.Label = value; break;
                case "placeholder": current.Placeholder = value; break;
                case "nameAttr": current.InputName = value; break;
                case "type": current.Type = value; break;
                case "href": current.Href = value; break;
            }
        }

        if (current is not null)
            list.Add(current);
        return list;
    }
}

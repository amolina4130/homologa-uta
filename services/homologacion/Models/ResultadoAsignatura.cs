    public const int MaxMateriasOrigen = 2;

    public bool Combinada { get; private set; }

    public List<MateriaOrigenResultado> MateriasOrigen { get; set; } = new();

    /// <summary>Reemplaza las materias de origen (1 o 2). Combinada es verdadera solo con dos.</summary>
    public void AsignarMateriasOrigen(IEnumerable<MateriaOrigenResultado> materias)
    {
        var lista = materias.ToList();

        if (lista.Count == 0)
            throw new ArgumentException("Debe haber al menos una materia de origen.");
        if (lista.Count > MaxMateriasOrigen)
            throw new ArgumentException("Una materia de destino admite como máximo dos materias de origen.");

        MateriasOrigen = lista;
        Combinada = lista.Count == 2;
    }
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class FileHandler : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void WriteToFile(string path, List<String> text)
    {
        System.IO.File.AppendAllLines(path, text);
    }

    public void overWriteFile(string path, string[] text)
    {
        System.IO.File.WriteAllLines(path, text);
    }

    public void MakeFileEmpty(string path)
    {
        using (FileStream fs = new FileStream(path, FileMode.Create))
        {
            // Do nothing, just open and close the file to make it empty
        }
    }
}

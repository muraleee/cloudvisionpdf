using Google.Apis.Auth.OAuth2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;
using Google.Cloud.Vision.V1;
using System.IO;
using Google.Protobuf;

namespace PDFConversion
{
    class Program
    {
        

        static void Main(string[] args)
        {
            //AuthImplicit("pdfconversion");
            string gcsSourceUri = "gs://pdf-conversion/Cargo Tonnage Report by Commodity118.pdf";
            string destinationBucket = "pdf-conversion";
            string destinationPrefix = "converted";
            DetectDocument(gcsSourceUri, destinationBucket, destinationPrefix);
        }

        public static object AuthImplicit(string projectId)
        {
            // If you don't specify credentials when constructing the client, the
            // client library will look for credentials in the environment.
            var credential = GoogleCredential.GetApplicationDefault();
            var storage = StorageClient.Create(credential);
            // Make an authenticated API request.
            var buckets = storage.ListBuckets(projectId);
            foreach (var bucket in buckets)
            {
                Console.WriteLine(bucket.Name);
            }
            return null;
        }

        public static object DetectDocument(string gcsSourceUri,
                string gcsDestinationBucketName, string gcsDestinationPrefixName)
        {
            var client = ImageAnnotatorClient.Create();

            var asyncRequest = new AsyncAnnotateFileRequest
            {
                InputConfig = new InputConfig
                {
                    GcsSource = new GcsSource
                    {
                        Uri = gcsSourceUri
                    },
                    // Supported mime_types are: 'application/pdf' and 'image/tiff'
                    MimeType = "application/pdf"
                },
                OutputConfig = new OutputConfig
                {
                    // How many pages should be grouped into each json output file.
                    BatchSize = 1,
                    GcsDestination = new GcsDestination
                    {
                        Uri = $"gs://{gcsDestinationBucketName}/{gcsDestinationPrefixName}/"
                    }
                }
            };

            asyncRequest.Features.Add(new Feature
            {
                Type = Feature.Types.Type.DocumentTextDetection
            });

            List<AsyncAnnotateFileRequest> requests =
                new List<AsyncAnnotateFileRequest>();
            requests.Add(asyncRequest);

            var operation = client.AsyncBatchAnnotateFiles(requests);

            Console.WriteLine("Waiting for the operation to finish");

            operation.PollUntilCompleted();

            // Once the rquest has completed and the output has been
            // written to GCS, we can list all the output files.
            var storageClient = StorageClient.Create();

            // List objects with the given prefix.
            var blobList = storageClient.ListObjects(gcsDestinationBucketName,
                gcsDestinationPrefixName);
            Console.WriteLine("Output files:");
            foreach (var blob in blobList)
            {
                Console.WriteLine(blob.Name);
            }

            // Process the first output file from GCS.
            // Select the first JSON file from the objects in the list.
            var output = blobList.Where(x => x.Name.Contains(".json")).First();

            var jsonString = "";
            using (var stream = new MemoryStream())
            {
                storageClient.DownloadObject(output, stream);
                jsonString = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }

            var response = JsonParser.Default
                        .Parse<AnnotateFileResponse>(jsonString);

            // The actual response for the first page of the input file.
            var firstPageResponses = response.Responses[0];
            var annotation = firstPageResponses.FullTextAnnotation;

            // Here we print the full text from the first page.
            // The response contains more information:
            // annotation/pages/blocks/paragraphs/words/symbols
            // including confidence scores and bounding boxes
            Console.WriteLine($"Full text: \n {annotation.Text}");

            return 0;
        }
    }
}

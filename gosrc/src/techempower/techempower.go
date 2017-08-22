package main

import "log"
import "net"
import (
	"crypto/x509"
	"crypto/x509/pkix"
	"crypto/rsa"
	"crypto/rand"
	"crypto/tls"
	"math/big"
	"time"
	"fmt"
	"encoding/pem"
	"os"
	"bufio"
)

const s_trace = false
const s_expectedReadSize = 2624

var s_responseMessage = []byte("HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n")

func handle_connection(c net.Conn) {
    buf := make([]byte, 4096)
    
	for {
		totalBytesRead := 0;
		for {
			bytesRead, err := c.Read(buf)
			if err != nil {
				if s_trace {
					log.Printf("connection read error, err=%s", err)
				}
				c.Close()
				return
			}
			
			if bytesRead == 0 {
				if s_trace {
					log.Printf("connection closed by client")
				}
				c.Close()
				return
			}
			
			if s_trace {
				log.Printf("read %n bytes", bytesRead)
			}

			totalBytesRead += bytesRead

			if totalBytesRead > s_expectedReadSize {
				log.Fatal("unexpected read size %n", bytesRead)
			}

			if totalBytesRead == s_expectedReadSize {
				break
			}
		}
		
		for i := 0; i < 16; i++ {
			bytesWritten, err := c.Write(s_responseMessage)
			if err != nil {
				if s_trace {
					log.Printf("connection write error, err=%s", err)
				}
				c.Close()
				return
			}
        
			if s_trace {
				log.Printf("wrote %n bytes", bytesWritten)
			}
		}
	}
}

func create_cert() (tlsCert tls.Certificate, err error) {
	// Code from: https://www.socketloop.com/tutorials/golang-create-x509-certificate-private-and-public-keys
	// Also: https://ericchiang.github.io/post/go-tls/

	// ok, lets populate the certificate with some data
	// not all fields in Certificate will be populated
	// see Certificate structure at
	// http://golang.org/pkg/crypto/x509/#Certificate
	template := &x509.Certificate {
			IsCA : true,
			BasicConstraintsValid : true,
			SubjectKeyId : []byte{1,2,3},
			SerialNumber : big.NewInt(1234),
			Subject : pkix.Name{
					Country : []string{"Earth"},
					Organization: []string{"Mother Nature"},
			},
	        SignatureAlgorithm : x509.SHA256WithRSA,
			NotBefore : time.Now(),
			NotAfter : time.Now().AddDate(5,5,5),
			// see http://golang.org/pkg/crypto/x509/#KeyUsage
			ExtKeyUsage : []x509.ExtKeyUsage{x509.ExtKeyUsageClientAuth, x509.ExtKeyUsageServerAuth},
			KeyUsage : x509.KeyUsageDigitalSignature|x509.KeyUsageCertSign,
	}

	// generate private key
	privatekey, err := rsa.GenerateKey(rand.Reader, 2048)

	if err != nil {
		fmt.Println(err)
		return
	}

	publickey := &privatekey.PublicKey

	// create a self-signed certificate. template = parent
	var parent = template
	certDER, err := x509.CreateCertificate(rand.Reader, template, parent, publickey, privatekey)

	if err != nil {
		fmt.Println(err)
		return
	}

	// cert, err := x509.ParseCertificate(certDER)

	// if err != nil {
	// 	fmt.Println(err)
	// 	return
	// }

    b := pem.Block{Type: "CERTIFICATE", Bytes: certDER}
    certPEM := pem.EncodeToMemory(&b)

	// PEM encode the private key
	privateKeyPEM := pem.EncodeToMemory(&pem.Block{
		Type: "RSA PRIVATE KEY", Bytes: x509.MarshalPKCS1PrivateKey(privatekey),
	})
	
	tlsCert, err = tls.X509KeyPair(certPEM, privateKeyPEM)
	if err != nil {
		log.Fatalf("invalid key pair: %v", err)
	}

	return 
}

func accept_connections(listener net.Listener) {
    for {
		// Wait for a connection.
		conn, err := listener.Accept()
		if err != nil {
			log.Fatal(err)
		}
        
		if s_trace {
	        log.Printf("Connection accepted")
		}
        
        go handle_connection(conn)
    }
}

func main() {
	// Listen on TCP port 5000 on all interfaces.
	listener, err := net.Listen("tcp", ":5000")
	if err != nil {
		log.Fatal(err)
	}

    go accept_connections(listener)

    log.Printf("Listening on *:5000")

    cert, err := create_cert()
	if err != nil {
		log.Fatal(err)
	}

	config := tls.Config{Certificates: []tls.Certificate{cert}}
	sslListener, err := tls.Listen("tcp", ":5001", &config)
	if err != nil {
		log.Fatal(err)
	}
	
    go accept_connections(sslListener)

    log.Printf("Listening on *:5001 (ssl)")

    log.Printf("Server running")

	reader := bufio.NewReader(os.Stdin)
	_, _ = reader.ReadString('\n')	
}